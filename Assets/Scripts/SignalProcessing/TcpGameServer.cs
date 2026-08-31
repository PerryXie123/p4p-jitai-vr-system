using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.SignalProcessing
{
    internal class TcpGameServer<T>
    {
        public string serverIP { get; set; } = "127.0.0.1";
        public int serverPort { get; set; } = 8081;

        private TcpListener listener;
        private TcpClient tcpClient;
        private Thread recievingThread;
        private Thread sendingThread;
        private NetworkStream stream;

        private volatile bool running;
        private volatile bool clientConnected;

        private readonly ConcurrentQueue<T> messageQueue;
        private readonly ConcurrentQueue<string> statusQueue;
        private readonly ConcurrentQueue<string> outgoingMessageQueue;
        private readonly AutoResetEvent outgoingMessageAvailable;
        private readonly object connectionLock = new object();

        public bool IsClientConnected => clientConnected;

        public TcpGameServer()
        {
            statusQueue = new ConcurrentQueue<string>();
            messageQueue = new ConcurrentQueue<T>();
            outgoingMessageQueue = new ConcurrentQueue<string>();
            outgoingMessageAvailable = new AutoResetEvent(false);
        }

        public void InitConnection()
        {
            try
            {
                if (running) return;

                running = true;
                recievingThread = new Thread(ListenToSocket) { IsBackground = true };
                sendingThread = new Thread(WriteToSocket) { IsBackground = true };
                recievingThread.Start();
                sendingThread.Start();
                statusQueue.Enqueue("Listening...");
            }
            catch (Exception e)
            {
                running = false;
                outgoingMessageAvailable.Set();
                Debug.LogError($"Failed to connect to server: {e.Message}");
                statusQueue.Enqueue($"Failed to connect to server: {e.Message}");
            }
        }

        public bool TryGetMessage(out T message)
        {
            return messageQueue.TryDequeue(out message);
        }

        public bool TryGetError(out string error)
        {
            return statusQueue.TryDequeue(out error);
        }

        public bool TrySend(T message)
        {
            if (!running || !clientConnected || ReferenceEquals(message, null))
            {
                return false;
            }

            string json;
            try
            {
                json = JsonUtility.ToJson(message) + "\n";
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to serialize outgoing JSON: {e.Message}");
                statusQueue.Enqueue($"Failed to serialize outgoing JSON: {e.Message}");
                return false;
            }

            // Enqueue while holding the same lock used to clear a disconnected
            // client, so a command cannot slip into the queue after cleanup and
            // then be delivered to a future connection.
            lock (connectionLock)
            {
                if (!running || !clientConnected)
                {
                    return false;
                }

                outgoingMessageQueue.Enqueue(json);
            }

            outgoingMessageAvailable.Set();
            return true;
        }

        private void ListenToSocket()
        {
            try
            {
                listener = new TcpListener(IPAddress.Parse(serverIP), serverPort);
                listener.Start();
                Debug.Log($"Listening for TCP connection on {serverIP}:{serverPort}");

                // Accept clients repeatedly so the server keeps working after a
                // client disconnects and later reconnects. One client at a time.
                while (running)
                {
                    ServeClient();
                }
            }
            catch (Exception e)
            {
                if (!running) return;

                Debug.LogError($"Error in TCP connection: {e.Message}");
                statusQueue.Enqueue($"Error in TCP connection: {e.Message}");
                running = false;
                outgoingMessageAvailable.Set();
            }
        }

        // Accepts one client and reads until it disconnects, then returns so the
        // listener can accept the next connection.
        private void ServeClient()
        {
            TcpClient connectedClient = null;
            NetworkStream connectedStream = null;

            try
            {
                connectedClient = listener.AcceptTcpClient();
                connectedClient.NoDelay = true;
                connectedStream = connectedClient.GetStream();

                lock (connectionLock)
                {
                    tcpClient = connectedClient;
                    stream = connectedStream;
                    clientConnected = true;
                }

                Debug.Log("Client connected");
                statusQueue.Enqueue("Client connected");
                ReadFromClient(connectedStream);
            }
            catch (Exception e)
            {
                if (!running) return;

                Debug.LogWarning($"Client connection lost, waiting for reconnect: {e.Message}");
            }
            finally
            {
                ClearConnection(connectedClient, connectedStream);

                if (connectedClient != null)
                {
                    statusQueue.Enqueue("Disconnected...");
                }
            }
        }

        private void WriteToSocket()
        {
            while (running)
            {
                outgoingMessageAvailable.WaitOne(250);

                while (running && outgoingMessageQueue.TryDequeue(out string message))
                {
                    NetworkStream connectedStream;
                    lock (connectionLock)
                    {
                        connectedStream = clientConnected ? stream : null;
                    }

                    if (connectedStream == null)
                    {
                        continue;
                    }

                    try
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(message);
                        connectedStream.Write(bytes, 0, bytes.Length);
                    }
                    catch (Exception e)
                    {
                        if (!running) return;

                        Debug.LogWarning($"Failed to send TCP message: {e.Message}");
                        statusQueue.Enqueue($"Failed to send TCP message: {e.Message}");
                        DisconnectCurrentClient(connectedStream);
                        break;
                    }
                }
            }
        }

        // Reads bytes from a connected client until it disconnects, dispatching
        // each complete newline-terminated message. `pending` starts empty for
        // every connection so a partial line cannot leak into the next one.
        private void ReadFromClient(NetworkStream clientStream)
        {
            byte[] buffer = new byte[4096];
            string pending = string.Empty;

            while (running)
            {
                int bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0)
                {
                    break;
                }

                pending += Encoding.UTF8.GetString(buffer, 0, bytesRead);
                pending = DispatchCompleteMessages(pending);
            }
        }

        // Enqueues each complete '\n'-terminated message and returns any partial
        // remainder to be completed by the next socket read.
        private string DispatchCompleteMessages(string pending)
        {
            int newlineIndex;
            while ((newlineIndex = pending.IndexOf('\n')) >= 0)
            {
                string message = pending.Substring(0, newlineIndex).Trim();
                pending = pending.Substring(newlineIndex + 1);

                if (message.Length == 0)
                {
                    continue;
                }

                try
                {
                    Debug.Log($"Received message: {message}");
                    T parsedMessage = ParseJsonToObject(message);
                    messageQueue.Enqueue(parsedMessage);
                }
                catch (Exception e)
                {
                    string error = $"Failed to dispatch TCP JSON frame: {e.Message}";
                    Debug.LogError(error);
                    statusQueue.Enqueue(error);
                }
            }

            return pending;
        }

        private T ParseJsonToObject(string json)
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse JSON: {e.Message}");
                throw;
            }
        }

        private void DisconnectCurrentClient(NetworkStream connectedStream)
        {
            TcpClient connectedClient = null;

            lock (connectionLock)
            {
                if (!ReferenceEquals(stream, connectedStream)) return;

                clientConnected = false;
                stream = null;
                connectedClient = tcpClient;
                tcpClient = null;
            }

            ClearOutgoingMessages();
            try { connectedStream?.Close(); } catch { }
            try { connectedClient?.Close(); } catch { }
        }

        private void ClearConnection(TcpClient connectedClient, NetworkStream connectedStream)
        {
            lock (connectionLock)
            {
                if (ReferenceEquals(stream, connectedStream))
                {
                    clientConnected = false;
                    stream = null;
                    tcpClient = null;
                }
            }

            ClearOutgoingMessages();
            try { connectedStream?.Close(); } catch { }
            try { connectedClient?.Close(); } catch { }
        }

        private void ClearOutgoingMessages()
        {
            while (outgoingMessageQueue.TryDequeue(out _))
            {
            }
        }

        public void CloseSocket()
        {
            running = false;
            clientConnected = false;
            outgoingMessageAvailable.Set();

            NetworkStream connectedStream;
            TcpClient connectedClient;
            lock (connectionLock)
            {
                connectedStream = stream;
                connectedClient = tcpClient;
                stream = null;
                tcpClient = null;
            }

            try { connectedStream?.Close(); } catch { }
            try { connectedClient?.Close(); } catch { }
            try { listener?.Stop(); } catch { }

            recievingThread?.Join(2500);
            sendingThread?.Join(2500);
            ClearOutgoingMessages();
            Debug.Log("Connection TCP Closed");
            statusQueue.Enqueue("Connection TCP Closed");
        }
    }
}

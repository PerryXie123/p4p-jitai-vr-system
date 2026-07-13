using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
        private NetworkStream stream;

        private volatile bool running;

        private readonly ConcurrentQueue<T> messageQueue;
        private readonly ConcurrentQueue<string> statusQueue;
        public delegate void OnReceivedCallback();
        private OnReceivedCallback onReceivedCallback;

        public TcpGameServer()
        {
            statusQueue = new ConcurrentQueue<string>();
            messageQueue = new ConcurrentQueue<T>();
        }


        public void InitConnection()
        {
            try
            {
                running = true;
                recievingThread = new Thread(ListenToSocket) { IsBackground = true };
                recievingThread.Start();
                statusQueue.Enqueue("Listening...");
            }
            catch (Exception e)
            {
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
            }
        }

        // Accepts a single client and reads from it until it disconnects, then
        // returns so ListenToSocket can accept the next one.
        private void ServeClient()
        {
            try
            {
                tcpClient = listener.AcceptTcpClient();
                stream = tcpClient.GetStream();
                Debug.Log("Client connected");
                statusQueue.Enqueue("Client connected");

                ReadFromClient(stream);
            }
            catch (Exception e)
            {
                if (!running) return;

                Debug.LogWarning($"Client connection lost, waiting for reconnect: {e.Message}");
            }
            finally
            {
                // Release this connection's sockets before re-accepting
                // so they are not leaked on every reconnect.
                try { stream?.Close(); } catch { }
                try { tcpClient?.Close(); } catch { }
                statusQueue.Enqueue("Disconnected...");

            }
        }

        // Reads bytes from a connected client until it disconnects, dispatching
        // each complete newline-terminated message. `pending` starts empty for
        // every connection so a half-received line from a dead client cannot
        // corrupt the next client's first message.
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

        // Pulls every complete '\n'-terminated message out of `pending`,
        // enqueuing each one, and returns any partial remainder.
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

                    if (onReceivedCallback != null)
                    {
                        onReceivedCallback();
                    }
                }
                catch
                {
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


        public void CloseSocket()
        {
            running = false;

            try { stream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            try { listener?.Stop(); } catch { }

            recievingThread?.Join(2500);
            Debug.Log("Connection TCP Closed");
            statusQueue.Enqueue("Connection TCP Closed");
        }

        public void SetOnReceivedCallback(OnReceivedCallback callback)
        {
            onReceivedCallback = callback;
        }
    }
}

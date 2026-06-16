using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private NetworkStream stream;

        private volatile bool running;

        private readonly ConcurrentQueue<T> messageQueue;
        private readonly ConcurrentQueue<string> statusQueue;

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
            } catch (Exception e)
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

                tcpClient = listener.AcceptTcpClient();
                stream = tcpClient.GetStream();
                Debug.Log("Client connected");
                statusQueue.Enqueue("Client connected");
                byte[] buffer = new byte[4096];
                string pending = string.Empty;

                while (running)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    pending += Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Each message is terminated by '\n'. Pull out every
                    // complete line; leave any partial remainder in `pending`.
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
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!running) return;
                
                Debug.LogError($"Error in TCP connection: {e.Message}");    
                statusQueue.Enqueue($"Error in TCP connection: {e.Message}");   
            }
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

    }
}

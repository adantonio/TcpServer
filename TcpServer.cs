using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Tester
{
    public class TcpServer
    {
        public class MessageReceivedEventArgs : EventArgs
        {
            public string Message { get; }
            public MessageReceivedEventArgs(string message)
            {
                Message = message;
            }
        }

        private ConcurrentDictionary<nint, string?> clientBuffer = new();
        private CancellationTokenSource cancellationToken = new();

        public event EventHandler OnConnected = delegate (object? sender, EventArgs e) { };
        public event EventHandler OnDisconnected = delegate (object? sender, EventArgs e) { };
        public event EventHandler OnError = delegate (object? sender, EventArgs e) { };
        public event EventHandler OnReceived = delegate (object? sender, EventArgs e) { };
        public event EventHandler OnStopped = delegate (object? sender, EventArgs e) { };

        private void InitializeClientBuffer(TcpClient client)
        {
            try
            {
                nint key = client.Client.Handle;
                if (!clientBuffer.ContainsKey(key))
                {
                    clientBuffer.AddOrUpdate(key, (nint _) => null, (_, _) => null);
                }
                else
                {
                    clientBuffer[key] = null;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new ErrorEventArgs(ex));
                client.Close();
            }
        }

        private async Task ReceiveData(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                while (client.Connected && !cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead != 0)
                    {
                        string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        lock (clientBuffer)
                        {
                            nint key = client.Client.Handle;
                            clientBuffer.AddOrUpdate(key, message, (_, exisitingValue) =>
                            {
                                if (exisitingValue is null)
                                {
                                    return message;
                                }
                                else
                                {
                                    message = exisitingValue + message;
                                    return message;

                                }
                            });
                        }
                        OnReceived?.Invoke(this, new MessageReceivedEventArgs(message));
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new ErrorEventArgs(ex));
                client.Close();
            }
            finally
            {
                OnDisconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RemoveClientBuffer(nint handle)
        {
            lock (clientBuffer)
            {
                if (clientBuffer.ContainsKey(handle))
                {
                    clientBuffer.Remove(handle, out _);
                }
            }
        }

        private async void HandleClient(TcpClient client, CancellationToken cancellationToken)
        {
            InitializeClientBuffer(client);
            await ReceiveData(client, cancellationToken);
            RemoveClientBuffer(client.Client.Handle);
        }

        public void Start(IPAddress ipAddress, int port)
        {
            TcpListener listener = new(ipAddress, port);
            listener.Start();
            Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    listener.AcceptTcpClientAsync(cancellationToken.Token).AsTask().ContinueWith((task) =>
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            TcpClient client = task.Result;

                            OnConnected?.Invoke(this, EventArgs.Empty);
                            HandleClient(client, cancellationToken.Token);
                        }
                    });
                }
            }, cancellationToken.Token).ContinueWith((task) =>
            {
                OnStopped?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Stop()
        {
            cancellationToken.Cancel();
        }

    }
}

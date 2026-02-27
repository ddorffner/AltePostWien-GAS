using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Linq; // Used in OscMessage for args ?? new object[0]

namespace YourOscLibrary
{
    public interface IOscSender : IDisposable
    {
        void Send(IOscMessage message);
        void Connect();
        void Close();
    }

    public interface IOscMessage
    {
        string Address { get; }
        object[] Arguments { get; }
    }

    public class OscSender : IOscSender
    {
        private string _ipAddress;
        private int _port;
        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;
        private bool _isConnected = false;

        public OscSender(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
            _udpClient = new UdpClient();
        }

        public void Connect()
        {
            try
            {
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
                _udpClient.Connect(_remoteEndPoint);
                _isConnected = true;
                Console.WriteLine($"OSC Sender connected to {_ipAddress}:{_port}");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Console.WriteLine($"Error connecting OSC Sender: {ex.Message}");
                throw;
            }
        }

        public void Send(IOscMessage message)
        {
            if (!_isConnected)
            {
                Console.WriteLine("OSC Sender not connected. Call Connect() first or ensure connection was successful.");
                return;
            }

            if (message == null)
            {
                Console.WriteLine("Cannot send a null OSC message.");
                return;
            }

            using (var memoryStream = new MemoryStream())
            {
                WriteOscString(memoryStream, message.Address);

                if (message.Arguments == null || message.Arguments.Length == 0)
                {
                    WriteOscString(memoryStream, ",");
                }
                else
                {
                    var typeTagString = new StringBuilder(",");
                    foreach (var arg in message.Arguments)
                    {
                        if (arg is float) typeTagString.Append('f');
                        else if (arg is int) typeTagString.Append('i');
                        else if (arg is string) typeTagString.Append('s');
                        else Console.WriteLine($"Unsupported OSC argument type: {arg.GetType()}. Skipping argument.");
                    }
                    WriteOscString(memoryStream, typeTagString.ToString());

                    foreach (var arg in message.Arguments)
                    {
                        if (arg is float floatVal) WriteOscFloat(memoryStream, floatVal);
                        else if (arg is int intVal) WriteOscInt(memoryStream, intVal);
                        else if (arg is string stringVal) WriteOscString(memoryStream, stringVal);
                    }
                }
                
                byte[] packet = memoryStream.ToArray();
                try
                {
                    _udpClient.Send(packet, packet.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending OSC packet: {ex.Message}");
                }
            }
        }

        private void WriteOscString(MemoryStream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
            PadToMultipleOfFour(stream);
        }

        private void WriteOscFloat(MemoryStream stream, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            stream.Write(bytes, 0, bytes.Length);
        }

        private void WriteOscInt(MemoryStream stream, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            stream.Write(bytes, 0, bytes.Length);
        }

        private void PadToMultipleOfFour(MemoryStream stream)
        {
            long remainder = stream.Position % 4;
            if (remainder > 0) for (int i = 0; i < 4 - remainder; i++) stream.WriteByte(0);
        }

        public void Close()
        {
            if (_isConnected)
            {
                _udpClient?.Close();
                _isConnected = false;
                Console.WriteLine("OSC Sender disconnected.");
            }
        }

        public void Dispose()
        {
            Close();
            _udpClient?.Dispose();
        }
    }

    public class OscMessage : IOscMessage
    {
        public string Address { get; private set; }
        public object[] Arguments { get; private set; }

        public OscMessage(string address, params object[] args)
        {
            Address = address;
            Arguments = args ?? new object[0];
        }
    }

    public interface IOscReceiver : IDisposable
    {
        event Action<IOscMessage> MessageReceived;
        void StartListening();
        void StopListening();
        bool IsListening { get; }
    }

    public class OscReceiver : IOscReceiver
    {
        private UdpClient _udpClient;
        private int _port;
        private volatile bool _isListening;
        private System.Threading.Thread _listeningThread;

        public event Action<IOscMessage> MessageReceived;
        public bool IsListening => _isListening;

        public OscReceiver(int port)
        {
            _port = port;
        }

        public void StartListening()
        {
            if (_isListening) return;
            try
            {
                _udpClient = new UdpClient(_port);
                _isListening = true;
                _listeningThread = new System.Threading.Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = $"OSCReceiver_Port{_port}"
                };
                _listeningThread.Start();
                Console.WriteLine($"OSC Receiver started listening on port {_port}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"OSC Receiver: Error starting listener on port {_port}: {ex.Message} (Port possibly in use)");
                _isListening = false;
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OSC Receiver: Generic error starting listener: {ex.Message}");
                _isListening = false;
                throw;
            }
        }

        private void ListenLoop()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            Console.WriteLine($"OSC Receiver: Listener thread running for port {_port}.");
            try
            {
                while (_isListening)
                {
                    try
                    {
                        byte[] receivedBytes = _udpClient.Receive(ref remoteEndPoint);
                        if (receivedBytes != null && receivedBytes.Length > 0)
                        {
                            IOscMessage message = ParsePacket(receivedBytes);
                            if (message != null) MessageReceived?.Invoke(message);
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (_isListening) Console.WriteLine($"OSC Receiver: SocketException in ListenLoop: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        if (_isListening) Console.WriteLine($"OSC Receiver: Error in ListenLoop: {ex.Message}");
                    }
                }
            }
            finally
            {
                 Console.WriteLine($"OSC Receiver: Listener thread for port {_port} terminating.");
            }
        }

        private IOscMessage ParsePacket(byte[] packet)
        {
            try
            {
                using (var memoryStream = new MemoryStream(packet))
                using (var reader = new BinaryReader(memoryStream))
                {
                    string address = ReadOscString(reader);
                    if (string.IsNullOrEmpty(address)) return null;

                    string typeTagString = ReadOscString(reader);
                    if (string.IsNullOrEmpty(typeTagString) || typeTagString[0] != ',')
                    {
                        Console.WriteLine($"OSC Receiver: Invalid type tag string: {typeTagString}");
                        return null;
                    }

                    List<object> arguments = new List<object>();
                    for (int i = 1; i < typeTagString.Length; i++)
                    {
                        char tag = typeTagString[i];
                        switch (tag)
                        {
                            case 'f': arguments.Add(ReadOscFloat(reader)); break;
                            case 'i': arguments.Add(ReadOscInt(reader)); break;
                            case 's': arguments.Add(ReadOscString(reader)); break;
                            default:
                                Console.WriteLine($"OSC Receiver: Unsupported type tag '{tag}' in message to {address}.");
                                return new OscMessage(address, arguments.ToArray());
                        }
                    }
                    return new OscMessage(address, arguments.ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OSC Receiver: Error parsing packet: {ex.Message}");
                return null;
            }
        }

        private string ReadOscString(BinaryReader reader)
        {
            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0) bytes.Add(b);
            long remainder = reader.BaseStream.Position % 4;
            if (remainder > 0) reader.ReadBytes((int)(4 - remainder));
            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        private float ReadOscFloat(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private int ReadOscInt(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bytes, 0));
        }

        public void StopListening()
        {
            if (!_isListening) return;
            _isListening = false;
            _udpClient?.Close();
            try
            {
                if (_listeningThread != null && _listeningThread.IsAlive)
                {
                    if (!_listeningThread.Join(TimeSpan.FromSeconds(1)))
                    {
                        Console.WriteLine("OSC Receiver: Listening thread did not terminate in time.");
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"OSC Receiver: Exception during StopListening > Thread.Join: {ex.Message}");
            }
            finally
            {
                 _udpClient?.Dispose();
                 _udpClient = null;
                 _listeningThread = null;
                 Console.WriteLine($"OSC Receiver stopped listening on port {_port}");
            }
        }

        public void Dispose()
        {
            StopListening();
        }
    }
} 
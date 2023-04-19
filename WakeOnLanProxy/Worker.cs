using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace WakeOnLanProxy;

public class Worker : BackgroundService
{
    static Queue<byte[]?> packetQueue = new Queue<byte[]?>();
    static bool isRunning = true;
    static AutoResetEvent packetAvailable = new AutoResetEvent(false);
    private readonly ILogger<Worker> _logger;
    private static int WoLPort = 40000;
    UdpClient? _receivingUdpClient = null;
    UdpState s = new UdpState();

    private ConcurrentDictionary<string, DateTimeOffset> _sendPacketToClientWaitList =
        new ConcurrentDictionary<string, DateTimeOffset>();

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }


    private struct UdpState
    {
        public UdpClient u;
        public IPEndPoint e;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        try
        {
            //Creates a UdpClient for reading incoming data.
           
            IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _receivingUdpClient = new UdpClient(WoLPort);
            
            // Start a worker thread to send the WOL packets
            Thread sendThread = new Thread( () => SendWorker(_logger));
            sendThread.Start();
            
            // Setup Udp Client 
            _logger.LogInformation("Starting WoL proxy");
            s.e = remoteIpEndPoint;
            s.u = _receivingUdpClient;
            _receivingUdpClient.BeginReceive(new AsyncCallback(UdpReceiveMessages), "Packet");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("running {LongTimeString}", DateTime.UtcNow.ToLongTimeString());
                stoppingToken.WaitHandle.WaitOne(30000);
            }
            
            _logger.LogInformation("done");
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
    }

    
    

    void UdpReceiveMessages(IAsyncResult ar)
    {
        // Get Data from Packet
        byte[] receiveBytes = s.u.EndReceive(ar, ref s.e);
        // Reinit the UDP client to Receive next packet 
        _receivingUdpClient?.BeginReceive(new AsyncCallback(UdpReceiveMessages), s);
        
        byte[] macAddress = new byte[6];
        string MacAddress = String.Empty;
        try
        {
            Array.Copy(receiveBytes, 6, macAddress, 0, 6);
            MacAddress = BitConverter.ToString(macAddress);
            _logger.LogInformation("MAC Address: {0}", MacAddress);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError("Error: {0}", ex.Message);
            return;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogError("Error: {0}", ex.Message);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {0}", ex.Message);
            return;
        }
        
       


        // Only accept one packet per MAC per 30 sec 
        DateTimeOffset lastPacketTimeStamp = DateTimeOffset.MinValue;
        _sendPacketToClientWaitList.TryGetValue(MacAddress, out lastPacketTimeStamp);

        if (lastPacketTimeStamp.AddSeconds(30) >= DateTimeOffset.UtcNow)
        {
            // discarding packet
            return;
        }
        // Update the Mac timestamp 
        _sendPacketToClientWaitList.AddOrUpdate(MacAddress,DateTimeOffset.Now, (key,oldvalue)=> DateTimeOffset.UtcNow);
        
        // Add the packet to the que
        lock (packetQueue)
        {
            packetQueue.Enqueue(receiveBytes);
        }
        
        // signal the sender there is a packet 
        packetAvailable.Set();
    }
    
    static void SendWorker(ILogger<Worker> logger)
    {
        // Create a Udp Sender 
        using (UdpClient sender = new UdpClient())
        {
            // If the packet needs to be send to the local LAN, then this needs to be done
            sender.EnableBroadcast = true;

            while (isRunning)
            {
                byte[]? packet = null;

                // Get packet from que
                lock (packetQueue)
                {
                    if (packetQueue.Count > 0)
                    {
                        packet = packetQueue.Dequeue();
                    }
                }

                if (packet != null)
                {
                    // Send the packet to the remote broadcast address
                    sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse("100.65.100.255"), WoLPort));
                }
                else
                {
                    // Wait for a packet to be enqueued
                    packetAvailable.WaitOne();
                }
            }
        }
    }
}
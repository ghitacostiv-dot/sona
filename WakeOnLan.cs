using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace SONA
{
    public class WolTarget
    {
        public string Name { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string BroadcastIp { get; set; } = "255.255.255.255";
        public int Port { get; set; } = 9;
    }

    public static class WakeOnLan
    {
        public static (bool Success, string Message) Send(string macAddress, string broadcastIp = "255.255.255.255", int port = 9)
        {
            try
            {
                var mac = ParseMac(macAddress);
                if (mac == null)
                    return (false, "Invalid MAC address format. Use XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX");

                var packet = new byte[6 + 16 * 6];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;
                for (int i = 1; i <= 16; i++)
                    for (int j = 0; j < 6; j++)
                        packet[i * 6 + j] = mac[j];

                using var client = new UdpClient();
                client.EnableBroadcast = true;
                var ep = new IPEndPoint(IPAddress.Parse(broadcastIp), port);
                client.Send(packet, packet.Length, ep);

                return (true, $"Magic packet sent to {macAddress} via {broadcastIp}:{port}");
            }
            catch (Exception ex) { return (false, $"Error: {ex.Message}"); }
        }

        public static (bool Success, string Message) Send(WolTarget target) =>
            Send(target.MacAddress, target.BroadcastIp, target.Port);

        private static byte[]? ParseMac(string mac)
        {
            mac = mac.Replace(":", "").Replace("-", "").Replace(" ", "").Trim();
            if (mac.Length != 12) return null;
            try
            {
                var bytes = new byte[6];
                for (int i = 0; i < 6; i++)
                    bytes[i] = Convert.ToByte(mac.Substring(i * 2, 2), 16);
                return bytes;
            }
            catch { return null; }
        }

        public static List<WolTarget> LoadTargets()
        {
            try
            {
                var json = AppConfig.GetString("wol_targets", "[]");
                return JsonConvert.DeserializeObject<List<WolTarget>>(json) ?? new List<WolTarget>();
            }
            catch { return new List<WolTarget>(); }
        }

        public static void SaveTargets(List<WolTarget> targets) =>
            AppConfig.Set("wol_targets", JsonConvert.SerializeObject(targets));
    }
}

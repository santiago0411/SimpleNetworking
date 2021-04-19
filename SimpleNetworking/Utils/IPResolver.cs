using System.Linq;
using System.Net;

namespace SimpleNetworking.Utils
{
    /// <summary>Contains helper methods to convert a host to a valid ip address.</summary>
    public static class IPResolver
    {
        /// <summary>Tries to resolve the host and return all the ipv4 addresses related to it.</summary>
        public static string[] GetIpv4AddressesFromHost(string host)
        {
            IPAddress[] ipv4Addresses = Dns.GetHostEntry(host).AddressList.Where(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToArray();
            string[] stringIps = new string[ipv4Addresses.Length];

            for (int i = 0; i < ipv4Addresses.Length; i++)
                stringIps[i] = ipv4Addresses[i].ToString();

            return stringIps;
        }

        /// <summary>Tries to resolve the host and return all the ipv6 addresses related to it.</summary>
        public static string[] GetIpv6AddressesFromHost(string host)
        {
            IPAddress[] ipv6Addresses = Dns.GetHostEntry(host).AddressList.Where(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6).ToArray();
            string[] stringIps = new string[ipv6Addresses.Length];

            for (int i = 0; i < ipv6Addresses.Length; i++)
                stringIps[i] = ipv6Addresses[i].ToString();

            return stringIps;
        }
    }
}

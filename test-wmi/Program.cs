using System;
using System.Management;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== NETWORK PERF DETAILED ===\n");
        
        try
        {
            var stats = new ManagementObjectSearcher("SELECT * FROM Win32_PerfFormattedData_Tcpip_NetworkInterface").Get().Cast<ManagementObject>().ToList();
            foreach (var s in stats)
            {
                Console.WriteLine("Name: " + s["Name"]);
                foreach (var prop in s.Properties)
                {
                    if (prop.Name.Contains("Bytes") || prop.Name.Contains("Packets") || prop.Name.Contains("Rate"))
                    {
                        Console.WriteLine("  " + prop.Name + " = " + prop.Value);
                    }
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}

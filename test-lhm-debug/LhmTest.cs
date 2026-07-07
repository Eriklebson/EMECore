using System;
using LibreHardwareMonitor.Hardware;

class Program
{
    static void Main()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsMotherboardEnabled = true,
            IsGpuEnabled = true
        };
        computer.Open();

        // Update all hardware multiple times
        for (int i = 0; i < 3; i++)
        {
            foreach (var hw in computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware)
                    sub.Update();
            }
            System.Threading.Thread.Sleep(500);
        }

        Console.WriteLine("=== CPU VOLTAGE SENSORS ===\n");

        foreach (var hw in computer.Hardware)
        {
            if (hw.HardwareType != HardwareType.Cpu) continue;
            
            Console.WriteLine("CPU: " + hw.Name);
            
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Voltage)
                {
                    Console.WriteLine("  [Voltage] " + sensor.Name + " = " + sensor.Value + " V");
                }
            }
        }

        Console.WriteLine("\n\n=== ALL CPU SENSORS ===\n");

        foreach (var hw in computer.Hardware)
        {
            if (hw.HardwareType != HardwareType.Cpu) continue;
            
            Console.WriteLine("CPU: " + hw.Name);
            
            foreach (var sensor in hw.Sensors)
            {
                Console.WriteLine("  [" + sensor.SensorType + "] " + sensor.Name + " = " + sensor.Value);
            }
        }

        Console.WriteLine("\n\n=== MOTHERBOARD DETAIL ===\n");

        foreach (var hw in computer.Hardware)
        {
            if (hw.HardwareType != HardwareType.Motherboard) continue;
            
            Console.WriteLine("Board: " + hw.Name);
            int subCount = 0;
            foreach (var _ in hw.SubHardware) subCount++;
            Console.WriteLine("SubHardware count: " + subCount);
            
            foreach (var sub in hw.SubHardware)
            {
                Console.WriteLine("");
                Console.WriteLine("  >> " + sub.Name);
                int sensorCount = 0;
                foreach (var _ in sub.Sensors) sensorCount++;
                Console.WriteLine("     Sensors count: " + sensorCount);
                foreach (var sensor in sub.Sensors)
                {
                    Console.WriteLine("    [" + sensor.SensorType + "] " + sensor.Name + " = " + sensor.Value);
                }
            }
        }

        Console.WriteLine("\n\n=== ALL FANS (all levels) ===\n");
        FindFans(computer.Hardware);

        computer.Close();
    }

    static void FindFans(System.Collections.Generic.IEnumerable<IHardware> hardware)
    {
        foreach (var hw in hardware)
        {
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Fan)
                    Console.WriteLine("FAN: " + sensor.Name + " = " + sensor.Value + " RPM [" + hw.Name + "]");
            }
            foreach (var sub in hw.SubHardware)
                FindFans(new[] { sub });
        }
    }
}


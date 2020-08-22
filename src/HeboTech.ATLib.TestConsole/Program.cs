﻿using HeboTech.ATLib.Communication;
using HeboTech.ATLib.Modems;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace HeboTech.ATLib.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TimeService.SetProvider(new SystemTimeProvider());

            using (SerialPort serialPort = new SerialPort("COM7", 9600, Parity.None, 8, StopBits.One))
            {
                serialPort.ReadTimeout = 1_000;
                Console.WriteLine("Opening serial port...");
                serialPort.Open();
                Console.WriteLine("Serialport opened");

                ICommunicator comm = new SerialPortCommunicator(serialPort);

                AdafruitFona modem = new AdafruitFona(comm);
                modem.IncomingCall += Modem_IncomingCall;
                modem.MissedCall += Modem_MissedCall;

                var simStatus = modem.GetSimStatus();
                Console.WriteLine($"SIM Status: {simStatus}");

                var signalStrength = modem.GetSignalStrength();
                Console.WriteLine($"Signal Strength: {signalStrength}");
                
                var batteryStatus = modem.GetBatteryStatus();
                Console.WriteLine($"Battery Status: {batteryStatus}");

                //var smsReference = modem.SendSMS(new PhoneNumber("<number>"), "Hello ATLib!");
                //Console.WriteLine($"SMS Reference: {smsReference}");

                //Thread.Sleep(30_000);
                modem.Close();
            }

            Console.WriteLine("Done. Press any key to exit...");
            Console.ReadKey();
        }

        private static void Modem_MissedCall(object sender, Events.MissedCallEventArgs e)
        {
            Console.WriteLine($"Missed call at {e.Time} from {e.PhoneNumber}");
        }

        private static void Modem_IncomingCall(object sender, Events.IncomingCallEventArgs e)
        {
            Console.WriteLine("Incoming call...");
        }
    }
}

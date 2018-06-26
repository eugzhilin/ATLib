﻿using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HeboTech.ATLib
{
    public class Gsm : IGsm
    {
        private readonly IGsmStream stream;
        private readonly int writeDelayMs = 25;
        private const string OK_RESPONSE = "\r\nOK\r\n";

        public Gsm(IGsmStream stream, int writeDelayMs = 25)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (writeDelayMs < 0)
                throw new ArgumentOutOfRangeException($"{nameof(writeDelayMs)} must be a positive number");
            this.writeDelayMs = writeDelayMs;
        }

        public Task<bool> InitializeAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                return stream.SendCheckReply("AT\r\n", OK_RESPONSE, 100);
            });
        }

        public Task<bool> SetModeAsync(Mode mode)
        {
            return Task.Factory.StartNew(() =>
            {
                return stream.SendCheckReply($"AT+CMGF={(int)mode}\r\n", OK_RESPONSE, 5_000);
            });
        }

        public Task<bool> SendSmsAsync(PhoneNumber phoneNumber, string message)
        {
            return Task.Factory.StartNew(() =>
            {
                bool status = false;
                status = stream.SendCheckReply($"AT+CMGS=\"{phoneNumber.ToString()}\"\r", "> ", 5_000);
                if (status)
                {
                    Thread.Sleep(writeDelayMs);
                    status = stream.SendCheckReply($"{message}\x1A\r\n", OK_RESPONSE, 180_000);
                }
                return status;
            });
        }

        public Task<bool> UnlockSimAsync(Pin pin)
        {
            return Task.Factory.StartNew(() =>
            {
                return stream.SendCheckReply($"AT+CPIN={pin.ToString()}", OK_RESPONSE, 20_000);
            });
        }

        public Task<BatteryStatus> GetBatteryStatusAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                string reply = stream.SendGetReply($"AT+CBC\r\n", OK_RESPONSE, 100);
                Match match = Regex.Match(reply, @"\+CBC: \d,\d*");
                if (match.Success)
                {
                    string[] numberStrings = match.Value.Substring(6).Split(',');
                    int batteryChargeStatus = Convert.ToInt32(numberStrings[0]);
                    double batteryChargeLevel = Convert.ToDouble(numberStrings[1]);
                    return new BatteryStatus((BatteryChargeStatus)batteryChargeStatus, batteryChargeLevel);
                }
                return null;
            });
        }
    }
}

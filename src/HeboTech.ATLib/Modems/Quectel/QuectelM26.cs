using HeboTech.ATLib.CodingSchemes;
using HeboTech.ATLib.DTOs;
using HeboTech.ATLib.Extensions;
using HeboTech.ATLib.Modems.SIMCOM;
using HeboTech.ATLib.Parsers;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HeboTech.ATLib.Modems.Quectel
{
    public class QuectelM26 : SIM5320, IModem, IQuectelM26
    {
        /// <summary>
        /// Based on SIMCOM SIM5320 chipset
        /// 
        /// Serial port settings:
        /// 9600 8N1 Handshake.None
        /// </summary>
        public QuectelM26(IAtChannel channel)
            : base(channel)
        {
        }

        public async Task<ModemResponse<Imei>> GetImeiAsync()
        {
            AtResponse response = await channel.SendSingleLineCommandAsync("AT+GSN", string.Empty);

            if (response.Success)
            {
                string line = response.Intermediates.FirstOrDefault() ?? string.Empty;
                var match = Regex.Match(line, @"(?<imsi>\d+)");
                if (match.Success)
                {
                    string imsi = match.Groups["imsi"].Value;
                    return ModemResponse.IsResultSuccess(new Imei(imsi));
                }
            }
            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasResultError<Imei>(error);
        }

        public override async Task<bool> SetRequiredSettingsBeforePinAsync()
        {
            ModemResponse echo = await DisableEchoAsync();
            ModemResponse errorFormat = await SetErrorFormatAsync(1);
            return echo.Success && errorFormat.Success;
        }

        public override async Task<bool> SetRequiredSettingsAfterPinAsync()
        {
            ModemResponse currentCharacterSet = await SetCharacterSetAsync(CharacterSet.UCS2);
            ModemResponse smsMessageFormat = await SetSmsMessageFormatAsync(SmsTextFormat.PDU);
            return currentCharacterSet.Success && smsMessageFormat.Success;
        }

        public override Task<ModemResponse> SendUssdAsync(string code, int codingScheme = 15)
        {
            return base.SendUssdAsync(EncodePDU.RawEncode(code), codingScheme);
        }


        public async virtual Task<ModemResponse> SendRawAsync(string commandText)
        {
            AtResponse response = await channel.SendCommand(commandText);

            if (response.Success)
                return ModemResponse.IsSuccess();

            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasError(error);
        }
    }
}
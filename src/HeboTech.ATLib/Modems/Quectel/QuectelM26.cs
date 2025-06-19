using HeboTech.ATLib.CodingSchemes;
using HeboTech.ATLib.DTOs;
using HeboTech.ATLib.Events;
using HeboTech.ATLib.Extensions;
using HeboTech.ATLib.Modems.SIMCOM;
using HeboTech.ATLib.Parsers;
using HeboTech.ATLib.PDU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using UnitsNet;

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

        public virtual async Task<ModemResponse> DeleteReadSmsAsync()
        {
            AtResponse response = await channel.SendCommand($"AT+QMGDA=1");

            if (response.Success)
                return ModemResponse.IsSuccess();

            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasError(error);
        }

        public override async Task<bool> SetRequiredSettingsBeforePinAsync()
        {
            ModemResponse echo = await DisableEchoAsync();
            ModemResponse errorFormat = await SetErrorFormatAsync(1);
            ModemResponse detect = await TurnOffSimDetect();
            return echo.Success && errorFormat.Success;

        }

        public override async Task<bool> SetRequiredSettingsAfterPinAsync()
        {
            ModemResponse currentCharacterSet = await SetCharacterSetAsync(CharacterSet.UCS2);
            ModemResponse smsMessageFormat = await SetSmsMessageFormatAsync(SmsTextFormat.PDU);
            _ = await SetNewSmsIndicationAsync(0,0, 0, 0, 0);
            return currentCharacterSet.Success && smsMessageFormat.Success;
        }

        public override Task<ModemResponse<UssdResponseEventArgs>> SendUssdAsync(string code, int codingScheme =0)
        {
            return base.SendUssdAsync(EncodePDU.RawEncode(code), codingScheme);
        }
        public Task<ModemResponse<UssdResponseEventArgs>> SendUssdAsyncRaw(string code, int codingScheme = 0)
        {
            return base.SendUssdAsync(code, codingScheme);
        }

        public async virtual Task<ModemResponse> SendRawAsync(string commandText)
        {
            AtResponse response = await channel.SendCommand(commandText);

            if (response.Success)
                return ModemResponse.IsSuccess();

            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasError(error);
        }
        public async virtual Task<ModemResponse> TurnOffSimDetect()
        {
            AtResponse response = await channel.SendCommand("AT+QSIMSTAT=0");

            if (response.Success)
                return ModemResponse.IsSuccess();

            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasError(error);
        }

        public async Task<ModemResponse<string>> getOwnNumber()
        {
            try
            {
                AtResponse response = await channel.SendSingleLineCommandAsync("AT+CNUM", "+CNUM");

                if (response.Success)
                {
                    if (response.Intermediates.Count > 0)
                    {
                        string line = response.Intermediates.First();
                        var match = Regex.Match(line, @"\+CNUM:\s"".*"",""\+?(?<number>\d{4,})"",.*");
                        if (match.Success)
                        {
                            return ModemResponse.IsResultSuccess(match.Groups["number"].Value);
                        }
                    }
                    else
                    {
                        return ModemResponse.IsResultSuccess("");
                    }
                }
                AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
                return ModemResponse.HasResultError<string>(error);
            }
            catch (InvalidResponseException ex)
            {
                return ModemResponse.IsResultSuccess("");
            }
            catch (Exception ex)
            {
                if(ex.InnerException != null)
                {
                    ex=ex.InnerException;
                }
                return ModemResponse.HasResultError<string>(new Error(99, ex.StackTrace));
            }
        }

        public async Task<ModemResponse> removePin(string pin)
        {
            try
            {
                ModemResponse pinEntered = await base.EnterSimPinAsync(new PersonalIdentificationNumber(pin));

                if (pinEntered?.Success ?? false)
                {
                    AtResponse response = await channel.SendCommand($"AT+CLCK=\"SC\",0,\"{pin}\"");

                    if (response.Success)
                    {
                        return ModemResponse.IsSuccess(true);
                    }
                    AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
                    return ModemResponse.HasResultError<string>(error);
                }
                else
                {
                    return pinEntered;
                }
            }
            catch (InvalidResponseException ex)
            {
                return ModemResponse.IsResultSuccess("");
            }
            catch (Exception ex)
            {
                return ModemResponse.HasResultError<string>(new Error(99, ex.Message));
            }
        }
        public async Task<ModemResponse<PhoneBookContent>> ReadPhoneBook(PhoneBookEntry phoneBook)
        {

            _ = await SetActivePhoneBookEntryAsync(phoneBook);

            AtResponse response = await channel.SendSingleLineCommandAsync("AT+CPBS?", "+CPBS");

            if (response.Success)
            {
                string line = response.Intermediates.FirstOrDefault() ?? string.Empty;
                var match = Regex.Match(line, @"""(?<fb>\w+)"",(?<used>\d+),(?<total>\d+)");
                if (match.Success)
                {
                    string used = match.Groups["used"].Value;
                    string total = match.Groups["total"].Value;

                    PhoneBookContent content = new PhoneBookContent()
                    {
                        Capacity = int.Parse(total),
                        Used = int.Parse(used)
                    };

                    return ModemResponse.IsResultSuccess(content);
                }
            }
            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasResultError<PhoneBookContent>(error);
        }
        public async Task<ModemResponse<PhoneBookRecord>> ReadPhoneBookRecordAsync(int index)
        {
            AtResponse response = await channel.SendSingleLineCommandAsync($"AT+CPBR={index}", "+CPBR");

            if (response.Success)
            {
                if (response is AtResponseEmpty)
                {
                    return ModemResponse.IsResultSuccess(new PhoneBookRecord() { Index = index });
                }
                string line = response.Intermediates.FirstOrDefault() ?? string.Empty;
                if (line != string.Empty)
                {
                    var match = Regex.Match(line, @"(?<index>\d+),""(?<number>[\*#\+\d]+)"",\d+,(""(?<title>.+)"")?");
                    if (match.Success)
                    {
                        string number = match.Groups["number"].Value;
                        var pe=new PhoneBookRecord()
                        {
                            Index = index,
                            Number = number,
                            Title = "noa"
                        };
                        var title = match.Groups["title"].Value;
                        try
                        {

                            pe.Title = EncodePDU.RawDecode(title);
                             
                        }
                        catch (Exception ex)
                        {
                            pe.Title = title;
                        }
                        return ModemResponse.IsResultSuccess(pe);

                    }
                }
                return ModemResponse.IsResultSuccess(new PhoneBookRecord()
                {
                    Index = index,
                    Number = "",
                    Title = ""
                });
            }
            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasResultError<PhoneBookRecord>(error);
        }

        public async Task<ModemResponse> OnOffModem(bool on)
        {
            AtResponse response = await channel.SendCommand("AT+CFUN="+(on?"1":"0"));

            if (response.Success)
                return ModemResponse.IsSuccess();

            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasError(error);

        }

        public virtual async Task<ModemResponse<SimRegistrationStatus>> GetRegistrationStatus()
        {

            AtResponse response = await channel.SendSingleLineCommandAsync($"AT+CREG?", "+CREG");

            if (response.Success)
            {
                if (response.Intermediates.Count > 0)
                {
                    try
                    {
                        var matched = Regex.Match(response.Intermediates[0], @"^\+CREG:\s\d,(?<regstatus>\d)");
                        if (matched.Success)
                        {
                            string cstatResult = matched.Groups["regstatus"].Value;
                            return cstatResult switch
                            {
                                "0" => ModemResponse.IsResultSuccess(SimRegistrationStatus.NOT_REGISTERED),
                                "1" => ModemResponse.IsResultSuccess(SimRegistrationStatus.REGISTERED_HOME),
                                "2" => ModemResponse.IsResultSuccess(SimRegistrationStatus.IN_REGISTRATION),
                                "3" => ModemResponse.IsResultSuccess(SimRegistrationStatus.REGISTRATION_DENIED),
                                "4" => ModemResponse.IsResultSuccess(SimRegistrationStatus.UNKNOWN),
                                "5" => ModemResponse.IsResultSuccess(SimRegistrationStatus.REGISTERED_ROAMING),
                                _ => ModemResponse.IsResultSuccess(SimRegistrationStatus.UNKNOWN)
                            };
                        }
                        return ModemResponse.HasResultError<SimRegistrationStatus>(new Error(99, "Not registered"));
                    }
                    catch (Exception e)
                    {
                        return ModemResponse.HasResultError<SimRegistrationStatus>(new Error(99, e.Message));
                    }

                }
                return ModemResponse.HasResultError<SimRegistrationStatus>(new Error(99,"No response"));
            }

            AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
            return ModemResponse.HasResultError<SimRegistrationStatus>(error);
        }


      
        public virtual async Task<IEnumerable<ModemResponse<SmsReference>>> SendSmsTextAsync(string phoneNumber, string message)
        {
            if (phoneNumber is null)
                throw new ArgumentNullException(nameof(phoneNumber));
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            List<ModemResponse<SmsReference>> references = new List<ModemResponse<SmsReference>>();
            var setFormat=await base.SetSmsMessageFormatAsync(SmsTextFormat.Text);
            try
            {
               
                    AtResponse response = await channel.SendSmsAsync($"AT+CMGS=\"{phoneNumber}\"", message, "+CMGS:");
                    if (response.Success)
                    {
                        string line = response.Intermediates.First();
                        var match = Regex.Match(line, @"\+CMGS:\s(?<mr>\d+)");
                        if (match.Success)
                        {
                            int mr = int.Parse(match.Groups["mr"].Value);
                            references.Add(ModemResponse.IsResultSuccess(new SmsReference(mr)));
                        }
                    }
                    else
                    {
                        if (AtErrorParsers.TryGetError(response.FinalResponse, out Error error))
                            references.Add(ModemResponse.HasResultError<SmsReference>(error));
                    }
                
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Error sending SMS: {ex.Message}");
            }
            finally
            {
               
            }
            return references;

           
        }

        public async Task<ModemResponse<SmsReadResult>> ListSmssAsync(SmsStatus smsStatus)
        {
            string command = $"AT+CMGL={(int)smsStatus}";

            AtResponse response = await channel.SendMultilineCommand(command, null);

            SmsReadResult result = new SmsReadResult();


            if (response.Success)
            {
                if ((response.Intermediates.Count % 2) != 0)
                    return ModemResponse.HasResultError<SmsReadResult>(new Error(999,"Uneven intermediates"));

                for (int i = 0; i < response.Intermediates.Count; i += 2)
                {
                    string metaDataLine = response.Intermediates[i];
                    string messageLine = response.Intermediates[i + 1];
                    try
                    {
                       
                       
                        var match = Regex.Match(metaDataLine, @"\+CMGL:\s(?<index>\d+),(?<status>\d+),""?""?,(?<length>\d+)");
                        if (match.Success)
                        {
                            int index = int.Parse(match.Groups["index"].Value);
                            SmsStatus status = (SmsStatus)int.Parse(match.Groups["status"].Value);
                            // Sent when AT+CSDH=1 is set
                            int length = int.Parse(match.Groups["length"].Value);
                            SmsDeliver sms = SmsDeliverDecoder.Decode(messageLine.ToByteArray());
                            result.Add(new SmsWithIndex(index, status, sms.SenderNumber, sms.Timestamp, sms.Message));
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AddError(new SmsUnrecogized() { Error = ex, RawCode = $"Meta:{metaDataLine} Data: {messageLine}" });

                    }
                }
                return ModemResponse.IsResultSuccess(result);
            }
            else
            {
                AtErrorParsers.TryGetError(response.FinalResponse, out Error error);
                return ModemResponse.HasResultError<SmsReadResult>(error);

            }
        }



    }

    public struct SmsUnrecogized
    {
        public Exception Error { get; set; }
        public string RawCode { get; set; }
    }
    public class SmsReadResult
    {
        private List<SmsUnrecogized> smsUnrecogizeds;

        private List<SmsWithIndex> smss;

        public SmsReadResult(List<SmsUnrecogized> smsUnrecogizeds, List<SmsWithIndex> smss)
        {
            this.smsUnrecogizeds = smsUnrecogizeds;
            this.smss = smss;
        }

        public SmsReadResult()
        {
            this.smsUnrecogizeds = new List<SmsUnrecogized>();
            this.smss =new List<SmsWithIndex>();

        }

        public List<SmsWithIndex> Smss => smss;
        public List<SmsUnrecogized> SmsUnrecogizeds=>smsUnrecogizeds;

        public void Add(SmsWithIndex sms)
        {
            this.smss.Add(sms);
        }
        public void AddError(SmsUnrecogized sms)
        {
            this.smsUnrecogizeds.Add(sms);
        }
    }

}






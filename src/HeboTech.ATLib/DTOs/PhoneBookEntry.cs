using System;
using System.Collections.Generic;
using System.Text;

namespace HeboTech.ATLib.DTOs
{
    public enum PhoneBookEntry
    {
        ON,
        SM

    }

    public class PhoneBookContent
    {
        public int Used;
        public int Capacity;
    }

    public class PhoneBookRecord
    {

        public int Index { get; set; }
        public string Number { get; set; }
        public string Title { get; set; }
    }
}

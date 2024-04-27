using System;
using System.Collections.Generic;
using System.Text;

namespace HeboTech.ATLib.DTOs
{

    public enum OperatorFormat
    {
        Long = 0,// Long format alphanumeric <oper>; can be up to 16 characters long
        Short = 1, //Short format alphanumeric <oper>
        Numeric = 2 // Numeric <oper>; GSM Location Area Identification number

    }

     public enum OperatorMode
    {
        Unknown=0, 
        Awailable=1,// Operator available
        Current=2, //Operator current
        Forbidden=3 // Operator forbidden
    }



    public class OperatorEntry
    {
        public OperatorMode Mode {  get; set; }
        public string Operator { get; set; }

        public OperatorFormat Format { get; set; }
    }
}

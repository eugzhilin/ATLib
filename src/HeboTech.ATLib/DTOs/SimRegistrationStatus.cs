using System;
using System.Collections.Generic;
using System.Text;

namespace HeboTech.ATLib.DTOs
{
    /*
     <stat>  0  Not registered, ME is not currently searching a new operator to register to 
   1  Registered, home network 
   2  Not registered, but ME is currently searching a new operator to register to 
   3  Registration denied 
   4  Unknown 
 5  Registered, roaming 
<lac>       String type; two byte location area code in hexadecimal format 
<ci>       String type; two byte cell ID in hexadecimal format 
     */
    public enum SimRegistrationStatus
    {
        NOT_REGISTERED = 0,
        REGISTERED_HOME = 1,
        IN_REGISTRATION = 2,
        REGISTRATION_DENIED = 3,
        UNKNOWN = 4,
        REGISTERED_ROAMING = 5
    }
}

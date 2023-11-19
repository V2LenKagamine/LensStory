using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Util;

namespace LensstoryMod
{
    public class Lentills
    {
        public static string FindInArray(string needle, string[] haystack)
        {
            if (needle == null || haystack == null || haystack.Length <= 0)
                return "";

            foreach (string hay in haystack)
            {
                if (hay == needle || WildcardUtil.Match(hay, needle))
                    return hay;
            }

            return "";
        }
    }
}

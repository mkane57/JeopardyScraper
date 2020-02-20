using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;

namespace JeopardyScraper.JeopardyObjects
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RoundType
    {
        [EnumMember(Value = "Jeopardy")]
        Jeopardy,
        [EnumMember(Value = "Double Jeopardy")]
        Double_Jeopardy,
        [EnumMember(Value = "Final Jeopardy")]
        Final_Jeopardy
    }
}

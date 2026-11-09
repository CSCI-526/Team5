using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Team5.Rebuild.Analytics
{
    // Flat JSON objects only: string, number, bool or null values.
    public static class AnalyticsJson
    {
        public static string Object(IReadOnlyList<KeyValuePair<string, object>> fields)
        {
            var builder = new StringBuilder(256);
            builder.Append('{');
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0) builder.Append(',');
                AppendString(builder, fields[i].Key);
                builder.Append(':');
                AppendValue(builder, fields[i].Value);
            }
            return builder.Append('}').ToString();
        }

        public static string Batch(IReadOnlyList<string> events)
        {
            var builder = new StringBuilder(64 + events.Count * 256);
            builder.Append("{\"events\":[");
            for (var i = 0; i < events.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(events[i]);
            }
            return builder.Append("]}").ToString();
        }

        public static void AppendValue(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null: builder.Append("null"); break;
                case bool flag: builder.Append(flag ? "true" : "false"); break;
                case string text: AppendString(builder, text); break;
                case float number: AppendNumber(builder, number); break;
                case double number: AppendNumber(builder, number); break;
                case decimal number: builder.Append(number.ToString(CultureInfo.InvariantCulture)); break;
                case int _: case long _: case short _: case byte _: case uint _: case ulong _: case ushort _: case sbyte _:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
                default: AppendString(builder, Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            }
        }

        public static void AppendNumber(StringBuilder builder, double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number)) { builder.Append("null"); return; }
            var rounded = Math.Round(number, 3, MidpointRounding.AwayFromZero);
            builder.Append(rounded == 0 ? "0" : rounded.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public static void AppendString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (var c in text ?? "")
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    default:
                        // U+2028/U+2029 are valid JSON but break older JS string literals.
                        if (c < ' ' || c == '\u2028' || c == '\u2029') builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}

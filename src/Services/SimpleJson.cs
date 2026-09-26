using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimpleTodo.Services
{
    /// <summary>
    /// 极简 JSON 读写器，零第三方依赖（不引用任何 NuGet 包）。
    /// 解析结果为通用 DOM：Dictionary&lt;string, object&gt; / List&lt;object&gt; / string / bool / double / null。
    /// 遵循标准 JSON 语法，并额外容忍对象与数组中的尾随逗号，方便手工编辑数据文件。
    /// </summary>
    public static class SimpleJson
    {
        #region 解析

        /// <summary>解析 JSON 文本，失败时抛出 FormatException。</summary>
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");

            int index = 0;
            object value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
            {
                throw new FormatException("JSON 结尾存在多余内容（位置 " +
                    index.ToString(CultureInfo.InvariantCulture) + "）");
            }
            return value;
        }

        /// <summary>解析 JSON 文本，不抛异常。</summary>
        public static bool TryParse(string text, out object value, out string error)
        {
            value = null;
            error = null;
            try
            {
                value = Parse(text);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 内容不完整");

            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': ExpectLiteral(s, ref i, "true"); return true;
                case 'f': ExpectLiteral(s, ref i, "false"); return false;
                case 'n': ExpectLiteral(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // 跳过 '{'

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 对象未闭合");

                // 空对象，或尾随逗号后的 '}'
                if (s[i] == '}') { i++; return map; }
                if (s[i] != '"') throw new FormatException("对象的键必须是字符串（位置 " + Position(i) + "）");

                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("键之后缺少 ':'（位置 " + Position(i) + "）");
                i++;

                map[key] = ParseValue(s, ref i);

                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return map; }
                throw new FormatException("JSON 对象格式错误（位置 " + Position(i) + "）");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            List<object> list = new List<object>();
            i++; // 跳过 '['

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 数组未闭合");

                // 空数组，或尾随逗号后的 ']'
                if (s[i] == ']') { i++; return list; }

                list.Add(ParseValue(s, ref i));

                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw new FormatException("JSON 数组格式错误（位置 " + Position(i) + "）");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // 跳过起始引号
            StringBuilder sb = new StringBuilder();

            while (true)
            {
                if (i >= s.Length) throw new FormatException("JSON 字符串未闭合");

                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c < ' ') throw new FormatException("JSON 字符串包含未转义的控制字符（位置 " + Position(i - 1) + "）");
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) throw new FormatException("转义序列不完整");
                char escape = s[i++];
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("\\u 转义不完整");
                        sb.Append(ParseHex4(s, i));
                        i += 4;
                        break;
                    default:
                        throw new FormatException("无法识别的转义字符 '\\" + escape + "'");
                }
            }
        }

        private static char ParseHex4(string s, int start)
        {
            int value = 0;
            for (int k = 0; k < 4; k++)
            {
                char c = s[start + k];
                int digit;
                if (c >= '0' && c <= '9') digit = c - '0';
                else if (c >= 'a' && c <= 'f') digit = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') digit = c - 'A' + 10;
                else throw new FormatException("\\u 转义包含非法十六进制字符 '" + c + "'");
                value = value * 16 + digit;
            }
            return (char)value;
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;

            if (i < s.Length && s[i] == '-') i++;

            if (i >= s.Length || !IsDigit(s[i]))
                throw new FormatException("无法解析的数字（位置 " + Position(start) + "）");

            if (s[i] == '0')
            {
                i++;
                if (i < s.Length && IsDigit(s[i]))
                    throw new FormatException("JSON 数字不允许前导零（位置 " + Position(start) + "）");
            }
            else
            {
                while (i < s.Length && IsDigit(s[i])) i++;
            }

            if (i < s.Length && s[i] == '.')
            {
                i++;
                if (i >= s.Length || !IsDigit(s[i]))
                    throw new FormatException("小数点后缺少数字（位置 " + Position(start) + "）");
                while (i < s.Length && IsDigit(s[i])) i++;
            }

            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                if (i >= s.Length || !IsDigit(s[i]))
                    throw new FormatException("指数部分缺少数字（位置 " + Position(start) + "）");
                while (i < s.Length && IsDigit(s[i])) i++;
            }

            string raw = s.Substring(start, i - start);
            double number;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                throw new FormatException("无法解析的数字 '" + raw + "'（位置 " + Position(start) + "）");
            }
            return number;
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static void ExpectLiteral(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length ||
                string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("无法识别的字面量（位置 " + Position(i) + "）");
            }
            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { i++; continue; }
                break;
            }
        }

        private static string Position(int index)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        #endregion

        #region DOM 取值助手

        public static object Get(Dictionary<string, object> map, string key)
        {
            if (map == null || key == null) return null;
            object value;
            return map.TryGetValue(key, out value) ? value : null;
        }

        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static List<object> AsArray(object value)
        {
            return value as List<object>;
        }

        public static string AsString(object value, string fallback)
        {
            if (value == null) return fallback;
            string text = value as string;
            return text != null ? text : fallback;
        }

        public static bool AsBool(object value, bool fallback)
        {
            if (value is bool) return (bool)value;
            if (value is double) return Math.Abs((double)value) > double.Epsilon;
            return fallback;
        }

        public static int AsInt(object value, int fallback)
        {
            if (value is double)
            {
                double number = (double)value;
                if (number >= int.MinValue && number <= int.MaxValue) return (int)Math.Round(number);
            }
            return fallback;
        }

        public static double AsDouble(object value, double fallback)
        {
            if (value is double) return (double)value;
            return fallback;
        }

        #endregion

        #region 写入

        /// <summary>把字符串写成合法的 JSON 字符串字面量（含引号）。</summary>
        public static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value != null)
            {
                for (int k = 0; k < value.Length; k++)
                {
                    char c = value[k];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < ' ')
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        #endregion
    }
}

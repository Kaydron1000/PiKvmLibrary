using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PiKvmLibrary
{
    public static class AppTypeDetector
    {
        public static bool IsWpfApp() =>
            System.Windows.Application.Current != null;

        public static bool IsWinFormsApp() =>
            System.Windows.Forms.Application.MessageLoop;


        public static async Task<string> ReadStreamWithCancellationAsync(this StreamReader reader, CancellationToken token)
        {
            const int bufferSize = 4096;
            return await reader.ReadStreamWithCancellationAsync(bufferSize, token);
        }
        public static async Task<string> ReadStreamWithCancellationAsync(this StreamReader reader, int bufferSize, CancellationToken token)
        {
            var buffer = new char[bufferSize];
            var result = new StringBuilder();

            while (true)
            {
                // Start the read operation
                var readTask = reader.ReadAsync(buffer, 0, buffer.Length);

                // Wait for it to complete or cancel
                var completedTask = await Task.WhenAny(readTask, Task.Delay(Timeout.Infinite, token));

                if (completedTask == readTask)
                {
                    int charsRead = readTask.Result;
                    if (charsRead == 0)
                        break; // End of stream

                    result.Append(buffer, 0, charsRead);
                }
                else
                {
                    // Task.Delay triggered => cancellation requested
                    throw new OperationCanceledException(token);
                }
            }

            return result.ToString();
        }
        /// <summary>
        /// Converts a character to its web name key representation.
        /// See for valid key names https://github.com/pikvm/kvmd/blob/master/keymap.csv
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string CharToWebNameKey(this char c)
        {
            // Convert the character to its Unicode code point and format it as a hexadecimal string
            if (char.IsLetter(c))
            {
                return "Key" + c.ToString().ToUpper();
            }
            if (char.IsDigit(c))
            {
                return "Digit" + c.ToString();
            }
            else
            {
                switch (c)
                {
                    case ' ':
                        return "Space";
                    case '\n':
                        return "Enter";
                    case '\r':
                        return "Enter";
                    case '\t':
                        return "Tab";
                    case '-':
                        return "Minus";
                    case '=':
                        return "Equal";
                    case '[':
                        return "BracketLeft";
                    case ']':
                        return "BracketRight";
                    case '\\':
                        return "Backslash";
                    case ';':
                        return "Semicolon";
                    case '\'':
                        return "Quote";
                    case ',':
                        return "Comma";
                    case '.':
                        return "Period";
                    case '/':
                        return "Slash";
                    case '`':
                        return "Backquote"; // '`' is typically represented by the backquote key
                    case '~':
                        return "Backquote"; // '~' is typically represented by Shift + '`'
                    case '!':
                        return "Digit1"; // '!' is typically represented by Shift + 1
                    case '@':
                        return "Digit2"; // '@' is typically represented by Shift + 2
                    case '#':
                        return "Digit3"; // '#' is typically represented by Shift + 3
                    case '$':
                        return "Digit4"; // '$' is typically represented by Shift + 4
                    case '%':
                        return "Digit5"; // '%' is typically represented by Shift + 5
                    case '^':
                        return "Digit6"; // '^' is typically represented by Shift + 6
                    case '&':
                        return "Digit7"; // '&' is typically represented by Shift + 7
                    case '*':
                        return "Digit8"; // '*' is typically represented by Shift + 8
                    case '(':
                        return "Digit9"; // '(' is typically represented by Shift + 9
                    case ')':
                        return "Digit0"; // ')' is typically represented by Shift + 0
                    case '_':
                        return "Minus"; // '_' is typically represented by Shift + '-'
                    case '+':
                        return "Equal"; // '+' is typically represented by Shift + '='
                    case '{':
                        return "BracketLeft"; // '{' is typically represented by Shift + '['
                    case '}':
                        return "BracketRight"; // '}' is typically represented by Shift + ']'
                    case '|':
                        return "Backslash"; // '|' is typically represented by Shift + '\'
                    case ':':
                        return "Semicolon"; // ':' is typically represented by Shift + ';'
                    case '"':
                        return "Quote"; // '"' is typically represented by Shift + '''
                    case '<':
                        return "Comma"; // '<' is typically represented by Shift + ','
                    case '>':
                        return "Period"; // '>' is typically represented by Shift + '.'
                    case '?':
                        return "Slash"; // '?' is typically represented by Shift + '/'
                    default:
                        break;
                }
            }
            throw new ArgumentException($"Character '{c}' does not have a valid web name key representation. Please refer to the keymap.csv for valid key names.");
        }
    }
}

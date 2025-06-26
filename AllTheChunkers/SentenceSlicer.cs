using System;

namespace AllTheChunkers;

public static class SentenceSlicer
{
   private static readonly char[] EndOfSentence = ['.', '!', '?', '\n'];

   public static IEnumerable<string> Slice(string text)
   {
      int i = 0;
      while (i < text.Length)
      {
         int idx = text.IndexOfAny(EndOfSentence, i);
         if (idx == -1)
         {  // Found last sentence
            var str = text.AsSpan(i).Trim();
            i = text.Length;
            yield return str.ToString();
         }
         else if (idx == 0)
         {  // Ignore empty strings
            i++;
         }
         else if (idx > 0)
         {            
            if ((idx < text.Length - 2) && (text[idx] == '.') && (text[idx + 1] == '.') && (text[idx + 2] == '.'))
            {  // Found a '.'followed by other two '.', so count it as a "..."
               idx += 2;
            }

            var str = text.AsSpan(i, idx + 1 - i).Trim();
            i = idx + 1;

            if (str.Length == 0)
            {
               continue;
            }
            yield return str.ToString();
         }
      }
   }
}

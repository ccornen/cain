using System.IO;
using Packer.Core;

namespace Packer
{
    class Program
    {
        static void Main()
        {
            var encFunc = "function _(s){var res =[s.length];for(var idx=0,i=0,j=s.length-1;idx<s.length;idx++){res[idx%2==1?i++:j--]=s[idx];}return res.join('');}";

            var rootPath = @"H:\aca\ame\";
            var js = File.ReadAllText(rootPath + "amen.js");

            var stringReplace = js.ReplaceStrings("__");
            js = stringReplace.Value;
            var stringHeader = stringReplace.Key;

            js = js.RemoveComments();
            js = js.Shortest();

            var globalVars = js.GetGlobalVars();

            js = js.ReplaceFunctions(globalVars,rootPath+@"\map.txt"); // replace functions names and functions local variables

            js = js.ReplaceVars(globalVars,null,"ç"); // replace global vars

           /* js = js.RemoveComments();
            js = js.Shortest();*/

            js = stringHeader + encFunc + js;

            //js = js.GlobalEncode(2);


            File.WriteAllText(rootPath + "b.js", js);
        }
    }
}

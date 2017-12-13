using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Packer.Model;

namespace Packer.Core
{
    public static class Extensions
    {
        public static string RemoveComments(this string js)
        {
            js = js.RemoveComments("//", "\n");
            js = js.RemoveComments("/*", "*/");

            return js;
        }

        public static string RemoveComments(this string js, string templateStart, string templateEnd)
        {
            var currIdx = 0;
            do
            {
                currIdx = js.IndexOf(templateStart, currIdx);
                var end = js.IndexOf(templateEnd, currIdx + templateStart.Length);

                if (end == -1 || currIdx == -1)
                    currIdx = -1;
                else
                {
                    var left = js.Substring(0, currIdx);
                    var right = js.Substring(end + templateEnd.Length);

                    js = left + right;

                    currIdx += templateStart.Length;
                }

            } while (currIdx > 0 && currIdx < js.Length);

            return js;
        }

        public static string Encode(this string plainString)
        {
            var res = "";
            var i = 0;
            var j = plainString.Length - 1;

            while (i <= j)
            {
                res += plainString[j--];
                res += plainString[i++];
            }

            if (plainString.Length % 2 == 1)
            {
                res = res.Substring(0, res.Length - 1);
            }

            return res.Replace("'", "\\'");
        }

        public static string Decode(this string encString)
        {
            var res = encString.ToCharArray();
            var i = 0;
            var j = encString.Length - 1;
            for (var idx = 0; idx < encString.Length; idx++)
            {
                res[idx % 2 == 1 ? i++ : j--] = encString[idx];
            }

            return new string(res).Replace("'", "\\'");
        }

        public static KeyValuePair<string, string> ReplaceStrings(this string js, string globalStringVarName)
        {
            var replacements = new Dictionary<string, int>();

            var currIdx = 0;
            do
            {
                var decal = currIdx;
                var jsArray = js.Substring(decal).ToCharArray();

                var startSingle = jsArray.Select((v, i) => new { car = v, index = i }).FirstOrDefault(c => c.car == '\'' && (c.index == 0 || jsArray[c.index - 1] != '\\'));
                var startSingleIdx = startSingle != null ? startSingle.index : jsArray.Length;

                var startDouble = jsArray.Select((v, i) => new { car = v, index = i }).FirstOrDefault(c => c.car == '"' && (c.index == 0 || jsArray[c.index - 1] != '\\'));
                var startDoubleIdx = startDouble != null ? startDouble.index : jsArray.Length;


                var rawIdx = Math.Min(startSingleIdx, startDoubleIdx);

                currIdx = rawIdx + decal;

                var endChar = rawIdx == startSingleIdx ? '\'' : '"';
                var otherChar = endChar == '"' ? '\'' : '"';

                if (currIdx < js.Length)
                {
                    var endIdx = jsArray.Select((v, i) => new { car = v, index = i }).First(c => c.index > rawIdx && c.car == endChar && (c.index == 0 || jsArray[c.index - 1] != '\\')).index;

                    endIdx += decal;

                    var left = js.Substring(0, currIdx);
                    var right = js.Substring(endIdx + 1);

                    if (endIdx >= js.Length || js[endIdx + 1] != ':') // not an object property
                    {
                        var txt = js.Substring(currIdx + 1, endIdx - currIdx - 1);
           
                        if (!txt.Contains(");"))
                        {
                            txt = txt.Replace("\\" + endChar, otherChar.ToString(CultureInfo.InvariantCulture));

                            var encoded = txt.Encode();
                            var index = replacements.Values.Count;

                            if (replacements.ContainsKey(encoded))
                                index = replacements[encoded];
                            else
                            {
                                replacements.Add(encoded, index);
                            }

                            var textReplacement = string.Format("_(__[{0}])", index);

                            js = left + textReplacement + right;

                            currIdx += textReplacement.Length + 1;
                        }
                        else
                        {
                            currIdx = endIdx + 1;
                        }
                    }
                    else
                    {
                        currIdx = endIdx + 1;
                    }
                }

            } while (currIdx > 0 && currIdx < js.Length);

            var globalVarCode = replacements.Any() ? string.Format("var {0} = [{1}];", globalStringVarName, string.Join(",", replacements.Keys.Select(x => string.Format("'{0}'", x)))):"";
            return new KeyValuePair<string, string>(globalVarCode, js);
        }

        public static string Shortest(this string js)
        {
            var speCars = "{}()[]|&><*-+/\\!?=;,:".ToCharArray();

            js = js.Replace("  ", " ");

            js = js.Replace("\r\n", "");
            js = js.Replace("\t", "");
            foreach (var speCar in speCars)
            {
                var car = speCar + "";
                js = js.Replace(car + " ", car);
                js = js.Replace(" " + car, car);
            }

            return js;

        }

        public static string ReplaceFunctions(this string js, List<string> globalVars, string mapFile)
        {
            if(File.Exists(mapFile))
                File.Delete(mapFile);

            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞßàáâãäåæèéêëìíîïðñòóôõöøùúûüýþÿ";
            var template = "function ";
            var currIdx = 0;
            var functionsNames = new List<string>();
            var funcVars = new List<string>();
            do
            {
                currIdx = js.IndexOf(template, currIdx, StringComparison.Ordinal);
                var end = js.IndexOf("(", currIdx + template.Length, StringComparison.Ordinal);

                if (end == -1 || currIdx == -1)
                    currIdx = -1;
                else
                {
                    var name = js.Substring(currIdx + template.Length, end - currIdx - template.Length);
                    if (!string.IsNullOrEmpty(name))
                    {
                        functionsNames.Add(name);
                    }

                    var endParams = js.IndexOf(")", end, StringComparison.Ordinal);

                    var parameters = js.Substring(end + 1, (endParams) - end - 1).Split(',').Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x));

                    funcVars.AddRange(parameters);

                    currIdx += template.Length;
                }

            } while (currIdx > 0 && currIdx < js.Length);


            for (var i = 0; i < functionsNames.Count; i++)
            {
                var newName = i < chars.Length
                    ? chars[i].ToString(CultureInfo.InvariantCulture)
                    : chars[i % chars.Length].ToString(CultureInfo.InvariantCulture)
                        .PadRight((int)Math.Ceiling(i / (float)chars.Length), '_');

                newName += "_";

                File.AppendAllText(mapFile,string.Format("{0} -> {1}\n",functionsNames[i],newName));

                js = js.Replace(functionsNames[i] + "(", newName + "(");
                js = js.Replace(functionsNames[i] + ",", newName + ",");
            }

            var functions = js.GetFunctions();

            foreach (var function in functions)
            {
                function.Start = js.IndexOf(function.Code,StringComparison.Ordinal);
                function.End = function.Start + function.Code.Length;

                var toReplace = function.Code.GetVars();
                toReplace.AddRange(function.Parameters);
                function.Code = function.Code.ReplaceVars(toReplace, globalVars);

                var left = js.Substring(0, (function.Start));
                var right = js.Substring((function.End));

                js = left + function.Code + right;
            }
            return js;
        }

        public static List<Function> GetFunctions(this string js)
        {
            var template = "function ";
            var currIdx = 0;
            var functions = new List<Function>();
            do
            {
                var func = new Function();

                currIdx = js.IndexOf(template, currIdx, StringComparison.Ordinal);
                var end = js.IndexOf("(", currIdx + template.Length, StringComparison.Ordinal);

                if (end == -1 || currIdx == -1)
                    currIdx = -1;
                else
                {
                    var name = js.Substring(currIdx + template.Length, end - currIdx - template.Length);
                    func.Name = name;

                    var endParams = js.IndexOf(")", end, StringComparison.Ordinal);

                    var parameters = js.Substring(end + 1, (endParams) - end - 1).Split(',').Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x));

                    func.Parameters = parameters.ToList();
                    func.Start = currIdx;
                    // Search func
                    var open = 0;
                    int closeAc;
                    var close = 0;
                    var idxAc = end;
                    do
                    {
                        var openAc = js.IndexOf("{", idxAc, StringComparison.Ordinal);
                        closeAc = js.IndexOf("}", idxAc, StringComparison.Ordinal);

                        if (openAc == -1)
                            openAc = js.Length;

                        if (closeAc == -1)
                            closeAc = js.Length;

                        if (openAc < closeAc)
                            open++;
                        if (closeAc < openAc)
                            close++;

                        idxAc = Math.Min(openAc, closeAc) + 1;


                    } while (open != close && idxAc < js.Length);

                    func.End = closeAc;
                    func.Code = js.Substring(func.Start, (func.End - func.Start)+1);
                    functions.Add(func);

                    currIdx += template.Length;
                }

            } while (currIdx > 0 && currIdx < js.Length);



            return functions;
        }

        public static List<string> GetGlobalVars(this string js)
        {
            var functions = js.GetFunctions();
            var tmpJs = new string(js.ToCharArray());

            foreach (var func in functions)
            {
                tmpJs = tmpJs.Replace(func.Code, "");
            }

            var globalVars = tmpJs.GetVars();

            return globalVars;
        }

        public static List<string> GetVars(this string js)
        {
            var template = "var ";
            var currIdx = 0;
            var variables = new List<string>();

            do
            {
                currIdx = js.IndexOf(template, currIdx, StringComparison.Ordinal);
                var end = js.IndexOf(";", currIdx + template.Length, StringComparison.Ordinal);

                if (end == -1 || currIdx == -1)
                    currIdx = -1;
                else
                {
                    var prevUpperCar = currIdx > 0 ? (js[currIdx - 1] + "").ToUpperInvariant()[0] : ';';
                    if (prevUpperCar < 65 || prevUpperCar > 90)
                    {
                        var txt = js.Substring(currIdx + template.Length, end - currIdx - template.Length);
                        if (!string.IsNullOrEmpty(txt))
                        {
                            txt = txt.RemoveBloc("[", "]");
                            txt = txt.RemoveBloc("{", "}");
                            txt = txt.RemoveBloc("(", ")");

                            var equalitySplit = txt
                                .Split(',')
                                .Select(a=>a.Split('=').Where((x, idx) => idx % 2 == 0).ToList()).SelectMany(y=>y);

                            var list = equalitySplit.Select(x => x.Split(',').Last().Trim());
                            variables.AddRange(list);

                        }

                        currIdx = end + 1;
                    }
                    else
                    {
                        currIdx += template.Length;
                    }
                }

            } while (currIdx > 0 && currIdx < js.Length);

            return variables.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        }

        public static string RemoveBloc(this string txt, string openCar, string closeCar)
        {
            var open = 0;
            var close = 0;
            var idxSymbol = 0;
            var startSymbol = txt.IndexOf(openCar[0]);
            var openSymbol = startSymbol;
            if (openSymbol > -1)
            {
                int closeSymbol;
                do
                {
                    closeSymbol = txt.IndexOf(closeCar, idxSymbol, StringComparison.Ordinal);

                    if (openSymbol == -1)
                        openSymbol = txt.Length;

                    if (closeSymbol == -1)
                        closeSymbol = txt.Length;

                    if (openSymbol < closeSymbol)
                        open++;
                    if (closeSymbol < openSymbol)
                        close++;

                    idxSymbol = Math.Min(openSymbol, closeSymbol) + 1;
                    openSymbol = txt.IndexOf(openCar, idxSymbol, StringComparison.Ordinal);

                } while (open != close && idxSymbol < txt.Length);

                return txt.Substring(0, startSymbol) + txt.Substring(closeSymbol + 1);
            }

            return txt;
        }

        private static int varIdx = 0;
        public static string ReplaceVars(this string js, List<string> variables, List<string> varsToIgnore = null, string prefix = null)
        {
            if(varsToIgnore == null)
                varsToIgnore = new List<string>();

            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞßàáâãäåæèéêëìíîïðñòóôõöøùúûüýþÿ";

           // var varIdx = 0;
            variables.ForEach(var =>
            {
                string newVar;
                do
                {
                    newVar = varIdx < chars.Length
                    ? chars[varIdx].ToString(CultureInfo.InvariantCulture)
                    : chars[varIdx % chars.Length].ToString(CultureInfo.InvariantCulture)
                        .PadLeft((int)Math.Ceiling(varIdx / (float)chars.Length), '_');

                    if (!string.IsNullOrEmpty(prefix))
                        newVar += prefix;

                    varIdx++;
                } while (varsToIgnore.Contains(newVar));


                var startIdx = 1;
                do
                {
                    startIdx = js.IndexOf(var, startIdx, StringComparison.Ordinal);
                    if (startIdx > -1)
                    {
                        var prevCar = js[startIdx - 1].ToString(CultureInfo.InvariantCulture).ToUpperInvariant()[0];
                        var nextCar = js[startIdx + var.Length].ToString(CultureInfo.InvariantCulture).ToUpperInvariant()[0];

                        if (((prevCar < 65 || prevCar > 90) && (nextCar < 65 || nextCar > 90)) && prevCar != '.' & nextCar !='_')
                        {
                            var left = js.Substring(0, startIdx);
                            var right = js.Substring(startIdx + var.Length);

                            js = left + newVar + right;

                            startIdx += newVar.Length + 1;
                        }
                        else
                        {
                            startIdx += var.Length;
                        }
                    }

                } while (startIdx > -1 && startIdx < js.Length);
            });

            return js;
        }

        public static string GlobalEncode(this string js, int times = 1, int idx = 0)
        {
            js = js.Encode();

            var code = string.Format("eval(atob('{0}'));", js);

            if (++idx == times)
                return code;
            else
            {
                return code.GlobalEncode(times, idx);
            }
        }

    }
}

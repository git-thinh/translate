using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

/* 
 * AutoResxTranslator
 * by Salar Khalilzadeh
 * 
 * https://autoresxtranslator.codeplex.com/
 * Mozilla Public License v2

//IsBusy(true);
GoogleTranslate01.TranslateAsync(
    text, "en", "vi", string.Empty,
    (success, result, type) =>
    {
        //SetResult(result, type);
        //IsBusy(false);
        Console.WriteLine(text + "(" + type + "): " + result);
        Trace.WriteLine(text + "(" + type + "): " + result);
    });

 */

public class GoogleTranslate01
{
    private const string RequestUserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:55.0) Gecko/20100101 Firefox/55.0";
    private const string RequestGoogleTranslatorUrl = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&hl=en&dt=t&dt=bd&dj=1&source=icon&tk=467103.467103&q={2}";


    public delegate void TranslateCallBack(bool succeed, string result, string type);
    public static void TranslateAsync(
        string text,
        string sourceLng,
        string destLng,
        string textTranslatorUrlKey,
        TranslateCallBack callBack)
    {
        var request = CreateWebRequest(text, sourceLng, destLng, textTranslatorUrlKey);
        request.BeginGetResponse(
            TranslateRequestCallBack,
            new KeyValuePair<WebRequest, TranslateCallBack>(request, callBack));
    }

    public static bool Translate(
        string text,
        string sourceLng,
        string destLng,
        string textTranslatorUrlKey,
        out string result,
        out string type)
    {
        var request = CreateWebRequest(text, sourceLng, destLng, textTranslatorUrlKey);
        try
        {
            var response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                result = "Response is failed with code: " + response.StatusCode;
                type = string.Empty;
                return false;
            }

            using (var stream = response.GetResponseStream())
            {
                string _output, _type;
                var succeed = ReadGoogleTranslatedResult(stream, out _output, out _type);
                result = _output;
                type = _type;
                return succeed;
            }
        }
        catch (Exception ex)
        {
            result = ex.Message;
            type = string.Empty;
            return false;
        }
    }

    static WebRequest CreateWebRequest(
        string text,
        string lngSourceCode,
        string lngDestinationCode,
        string textTranslatorUrlKey)
    {
        text = HttpUtility.UrlEncode(text);

        var url = string.Format(RequestGoogleTranslatorUrl, lngSourceCode, lngDestinationCode, text);


        var create = (HttpWebRequest)WebRequest.Create(url);
        create.UserAgent = RequestUserAgent;
        create.Timeout = 50 * 1000;
        return create;
    }

    private static void TranslateRequestCallBack(IAsyncResult ar)
    {
        var pair = (KeyValuePair<WebRequest, TranslateCallBack>)ar.AsyncState;
        var request = pair.Key;
        var callback = pair.Value;
        HttpWebResponse response = null;
        try
        {
            response = (HttpWebResponse)request.EndGetResponse(ar);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                callback(false, "Response is failed with code: " + response.StatusCode, string.Empty);
                return;
            }

            using (var stream = response.GetResponseStream())
            {
                string output, type;
                var succeed = ReadGoogleTranslatedResult(stream, out output, out type);

                callback(succeed, output, type);
            }
        }
        catch (Exception ex)
        {
            callback(false, "Request failed.\r\n" + ex.Message, string.Empty);
        }
        finally
        {
            if (response != null)
            {
                response.Close();
            }
        }
    }

    /// <summary>
    ///  the main trick :)
    /// </summary>
    static bool ReadGoogleTranslatedResult(Stream rawdata, out string result, out string type)
    {
        string text;
        using (var reader = new StreamReader(rawdata, Encoding.UTF8))
        {
            text = reader.ReadToEnd();
        }

        try
        {
            result = string.Empty;
            type = string.Empty;

            AutoResxTranslator it = Newtonsoft.Json.JsonConvert.DeserializeObject<AutoResxTranslator>(text);
            if (it.sentences.Length > 0 && it.sentences[0].trans != null)
                result = it.sentences[0].trans;
            if (it.dict.Length > 0)
            {
                type = it.dict[0].pos;
                if (it.dict[0].terms != null && it.dict[0].terms.Length > 0)
                    result += "; " + string.Join("; ", it.dict[0].terms);
            }
            return true;
        }
        catch (Exception ex)
        {
            result = ex.Message;
            type = string.Empty;
            return false;
        }
    }

}

public class AutoResxTranslator
{
    public sentences[] sentences { set; get; }
    public dict[] dict { set; get; }
}

public class sentences
{
    public string trans { set; get; }
    public string orig { set; get; }
}

public class dict
{
    public string pos { set; get; }
    public string[] terms { set; get; }
}

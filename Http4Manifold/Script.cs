using Manifold;
using System;
using System.IO;
using System.Net;

public class Script
{
    private static readonly string AddinName = "Http4Manifold";
    private static readonly string AddinCodeFolder = "Code\\Http4Manifold";

    private static readonly string[] CodeFiles = { "Http4Manifold.sql" };


    // The current application context provided by Manifold at run time
    private static Context Manifold;

    // static constructor. 
    static Script()
    {
        System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }

    public static void Main()
    {
        Application app = Manifold.Application;

        using (Database db = app.GetDatabaseRoot())
        {
            CreateQueries(app, db);
        }

        app.Log(DisplayHelp());
        app.OpenLog();

    }



    private static string DisplayHelp()
    {
        return "Use include directive:\r\n-- $include$ [Http4Manifold.sql]";
    }


    public static string HttpGet(string url)
    {
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = 10000; // 10 seconds
    
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        catch (UriFormatException ex)
        {
            return "ERROR (UriFormatException): " + ex.Message;
        }
        catch (WebException ex)
        {
            using (HttpWebResponse errorResponse = ex.Response as HttpWebResponse)
            {
                if (errorResponse != null)
                {
                    using (Stream errStream = errorResponse.GetResponseStream())
                    using (StreamReader errReader = new StreamReader(errStream))
                    {
                        string errorDetails = errReader.ReadToEnd();
                        return $"ERROR: {ex.Message}\r\nServer returned: {errorDetails}";
                    }
                }
                else
                {
                    return "ERROR: " + ex.Message;
                }
            }
        }
        catch (Exception ex)
        {
            // Catch-all for any other unexpected exceptions
            return "ERROR (General Exception): " + ex.Message;
        }
    }


    public static string HttpPost(string uri, string postData, string contentType = "application/x-www-form-urlencoded")
    {
        try
        {
            // Create the web request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "POST";
        
            // If you want compression for the response:
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            // Set timeouts (optional)
            // request.Timeout = 10000; // 10s request timeout
            // request.ReadWriteTimeout = 10000; // 10s for reading/writing

            // Set content type
            // e.g. "application/json", "text/plain", "application/x-www-form-urlencoded", etc.
            request.ContentType = contentType;

            // Convert the data to bytes (assuming UTF-8)
            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(postData);
        
            // Set content length
            request.ContentLength = dataBytes.Length;

            // Write data to request body
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(dataBytes, 0, dataBytes.Length);
            }

            // Get the response
        
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        catch (UriFormatException ex)
        {
            return "ERROR (UriFormatException): " + ex.Message;
        }
        catch (WebException ex)
        {
            using (HttpWebResponse errorResponse = ex.Response as HttpWebResponse)
            {
                if (errorResponse != null)
                {
                    using (Stream errStream = errorResponse.GetResponseStream())
                    using (StreamReader errReader = new StreamReader(errStream))
                    {
                        string errorDetails = errReader.ReadToEnd();
                        return $"ERROR: {ex.Message}\r\nServer returned: {errorDetails}";
                    }
                }
                else
                {
                    return "ERROR: " + ex.Message;
                }
            }
        }
        catch (Exception ex)
        {
            // Catch-all for any other unexpected exceptions
            return "ERROR (General Exception): " + ex.Message;
        }
    }


    public static void CreateQuery(Application app, Database db, string name, string text, string folder = "")
    {
        PropertySet propertyset = app.CreatePropertySet();
        propertyset.SetProperty("Text", text);
        if (folder != "")
        {
            propertyset.SetProperty("Folder", folder);

        }
        db.Insert(name, "query", null, propertyset);

    }

    public static void CreateQueries(Application app, Database db)
    {
        string AddinDir = System.IO.Path.GetDirectoryName(new System.Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath);

        foreach (string fname in CodeFiles)
        {
            bool rewrite = true;

            if (db.GetComponentType(fname) == "")
            {
                rewrite = true;
            }
            else
            {
                rewrite = false;

                string message = $"{db.GetComponentType(fname).ToUpper()} {fname} already exists. DROP?";

                System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.YesNo;
                System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(message, AddinName, buttons);
                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    db.Delete(fname);
                    rewrite = true;
                }
            }

            if (rewrite)
            {
                string text = File.ReadAllText(AddinDir + "\\" + fname);

                // insert
                CreateQuery(app, db, fname, text, AddinCodeFolder);
            }
        }
    }


}

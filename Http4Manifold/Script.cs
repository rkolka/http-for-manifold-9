using Manifold;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class Script
{
    private static readonly string AddinName = "Http4Manifold";
    private static readonly string AddinCodeFolder = "Code\\Http4Manifold";

    private static readonly string[] CodeFiles = { "Http4Manifold.sql", "Http4Manifold-examples.sql" };


    // The current application context provided by Manifold at run time
    private static Context Manifold;

    // static constructor. 
    private static readonly HttpClient _httpClient;

    static Script()
    {
        System.Diagnostics.Debug.WriteLine("Dll loading");

        System.Diagnostics.Debug.WriteLine(Manifold is null);

        // Force TLS 1.2, if needed:
        System.Net.ServicePointManager.SecurityProtocol =
            System.Net.SecurityProtocolType.Tls12
            | System.Net.SecurityProtocolType.Tls11
            | System.Net.SecurityProtocolType.Tls;

        // Create the shared HttpClient instance
        _httpClient = new HttpClient();
        // Optionally set defaults, e.g.:
        // _httpClient.Timeout = TimeSpan.FromSeconds(10);
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
            // Wrap the async call in a sync call
            return HttpGetInternal(url).GetAwaiter().GetResult();
        }
        catch (UriFormatException ex)
        {
            return "ERROR (UriFormatException): " + ex.Message;
        }
        catch (Exception ex)
        {
            return "ERROR (General Exception): " + ex.Message;
        }
    }

    private static async Task<string> HttpGetInternal(string url)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(url);
        // Throw if not success
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static string HttpPost(string uri, string postData, string contentType = "application/x-www-form-urlencoded")
    {
        try
        {
            // Sync-over-async
            return HttpPostInternal(uri, postData, contentType).GetAwaiter().GetResult();
        }
        catch (UriFormatException ex)
        {
            return "ERROR (UriFormatException): " + ex.Message;
        }
        catch (Exception ex)
        {
            return "ERROR (General Exception): " + ex.Message;
        }
    }

    private static async Task<string> HttpPostInternal(string url, string postData, string contentType)
    {
        // Prepare content
        var dataBytes = new StringContent(postData, Encoding.UTF8, contentType);
        HttpResponseMessage response = await _httpClient.PostAsync(url, dataBytes);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
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

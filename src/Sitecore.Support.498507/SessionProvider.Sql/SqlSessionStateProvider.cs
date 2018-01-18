namespace Sitecore.Support.SessionProvider.Sql
{
  using System;
  using System.Collections.Specialized;
  using System.Diagnostics;
  using System.Linq;
  using System.Web;
  using System.Web.SessionState;

  using Newtonsoft.Json;

  using Sitecore.Diagnostics;
  using Sitecore.SessionProvider.Helpers;

  public class SqlSessionStateProvider : Sitecore.SessionProvider.Sql.SqlSessionStateProvider
  {
    public TimeSpan Threshold { get; set; } = TimeSpan.MaxValue;

    public bool LogSessionData { get; set; } = false;

    public override void Initialize(string name, NameValueCollection config)
    {
      Assert.ArgumentNotNull(name, "name");
      Assert.ArgumentNotNull(config, "config");

      base.Initialize(name, config);

      var configReader = new ConfigReader(config, name);
      var threshold = configReader.GetString("threshold", TimeSpan.MaxValue.ToString(), true);
      var logSessionData = configReader.GetBool("logSessionData", false);

      Threshold = TimeSpan.Parse(threshold);
      LogSessionData = logSessionData;
    }

    public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
    {
      var stopwatch = Stopwatch.StartNew();

      SessionStateStoreData result = null;
      try
      {
        result = base.GetItemExclusive(context, id, out locked, out lockAge, out lockId, out actions);

        stopwatch.Stop();

        return result;
      }
      finally
      {
        stopwatch.Stop();

        if (stopwatch.Elapsed > Threshold)
        {
          Log.Warn($"Execution {typeof(SqlSessionStateProvider).FullName}.{nameof(GetItemExclusive)} exceeded threshold: {stopwatch.Elapsed}, id: {id}, request: {context.Request.HttpMethod} {context.Request.RawUrl}{Serialize(result)}", this);
        }
      }
    }

    private string Serialize(SessionStateStoreData result)
    {
      if (!LogSessionData)
      {
        return string.Empty;
      }

      const string prefix = ", session_data: ";
      if (result?.Items == null)
      {
        return prefix + "[null]";
      }

      try
      {
        return prefix + JsonConvert.SerializeObject(
            result.Items
              .Keys.Cast<string>()
              .ToDictionary(x => x, x => result.Items[x]),
            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
      }
      catch
      {
        return prefix + "[cannot serialize]";
      }
    }
  }
}
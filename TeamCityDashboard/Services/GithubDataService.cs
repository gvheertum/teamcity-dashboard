﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using log4net;
using Newtonsoft.Json.Linq;
using TeamCityDashboard.Models;

namespace TeamCityDashboard.Services
{
  public class GithubDataService
  {
    private static readonly ILog log = LogManager.GetLogger(typeof(GithubDataService));
    
    private string oauth2token;
    private string eventsurl;
    private const string API_BASE_URL = @"https://api.github.com";

    public GithubDataService(string oauth2token, string eventsurl)
    {
      this.oauth2token = oauth2token;
      this.eventsurl = eventsurl;
    }

    public IEnumerable<TeamCityDashboard.Models.PushEvent> GetRecentEvents(bool ignoreEtag=false)
    {
      try
      {
        string response = GetEventsApiContents(ignoreEtag);
        if (!string.IsNullOrWhiteSpace(response))
        {
          JArray events = JArray.Parse(response);
          var parsedEvents= (from evt in events
                           where (string)evt["type"] == "PushEvent" 
                           select new PushEvent { 
                              RepositoryName = (string)evt["repo"]["name"],
                              BranchName = ((string)evt["payload"]["ref"]).Replace("refs/heads/", ""),
                              ActorUsername = (string)evt["actor"]["login"],
                              ActorGravatarId = (string)evt["actor"]["gravatar_id"],
                              AmountOfCommits = (int)evt["payload"]["size"],
                              Created = (DateTime)evt["created_at"]
                           }).ToList();
          if (parsedEvents.Any())
            log.DebugFormat("Retrieved {0} new push events from github (ignoredEtag={1})", parsedEvents.Count(), ignoreEtag);

          return parsedEvents;
        }
      }
      catch (Exception ex)
      {
        log.Error(ex);
      }
      return Enumerable.Empty<PushEvent>();
    }

    private string LastReceivedEventsETAG;
    protected string GetEventsApiContents(bool ignoreEtag)
    {
      try
      {
        Uri uri = new Uri(string.Format("{0}{1}", API_BASE_URL, eventsurl));
        HttpWebRequest myHttpWebRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
        myHttpWebRequest.UserAgent = "TeamCity CI Dashboard - https://github.com/crunchie84/teamcity-dashboard";
        myHttpWebRequest.Headers.Add("Authorization", "bearer " + oauth2token);

        if (!string.IsNullOrWhiteSpace(LastReceivedEventsETAG) && !ignoreEtag)
          myHttpWebRequest.Headers.Add("If-None-Match", LastReceivedEventsETAG);

        using (HttpWebResponse myWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse())
        {
          if (myWebResponse.StatusCode == HttpStatusCode.OK)
          {
            //if we do not save the returned ETag we will always get the full list with latest changes instead of the real delta since we started polling.
            LastReceivedEventsETAG = myWebResponse.Headers.Get("ETag");

            using (Stream responseStream = myWebResponse.GetResponseStream())
            {
              StreamReader myStreamReader = new StreamReader(responseStream, Encoding.Default);
              return myStreamReader.ReadToEnd();
            }
          }
        }
      }
      catch (HttpException ex)
      {
        if (ex.GetHttpCode() != (int)HttpStatusCode.NotModified)
          throw;
      }
      catch (WebException ex)
      {
        var response = ex.Response as HttpWebResponse;
        if(response == null || response.StatusCode != HttpStatusCode.NotModified)
          throw;
      }
      catch (Exception ex)
      {
        throw new HttpException(string.Format("Error while retrieving url '{0}': {1}", eventsurl, ex.Message), ex);
      }
      return string.Empty;
    }
  }
}

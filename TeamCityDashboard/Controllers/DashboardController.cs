﻿using ITCloud.Web.Routing;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using TeamCityDashboard.Interfaces;
using TeamCityDashboard.Services;

namespace TeamCityDashboard.Controllers
{
	public class DashboardController : Controller
	{
		private static TeamCityDataService TeamCityDataService;
		private static SonarDataService SonarDataService;
		private static GithubDataService GithubDataService;

		private static string _version;
		public string ProjectVersion
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_version))
				{
					_version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
					if (string.IsNullOrWhiteSpace(_version))
						_version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
				}
				return _version;
			}
		}

		public DashboardController()
		{
			ICacheService cacheService = new WebCacheService();

			//singletonish ftw
			if (TeamCityDataService == null)
			{
				TeamCityDataService = new TeamCityDataService(
				  ConfigurationManager.AppSettings["teamcity.baseUrl"],
				  ConfigurationManager.AppSettings["teamcity.username"],
				  ConfigurationManager.AppSettings["teamcity.password"],
				  cacheService
				);
			}

			//singletonish ftw
			if (SonarDataService == null)
			{
				SonarDataService = new SonarDataService(
				  ConfigurationManager.AppSettings["sonar.baseUrl"],
				  ConfigurationManager.AppSettings["sonar.username"],
				  ConfigurationManager.AppSettings["sonar.password"],
				  cacheService
				);
			}

			if (GithubDataService == null)
			{
				GithubDataService = new GithubDataService(
				  ConfigurationManager.AppSettings["github.oauth2token"],
				  ConfigurationManager.AppSettings["github.api.events.url"]
				  , cacheService
				);
			}
		}

		[UrlRoute(Name = "VersionString", Path = "version")]
		[HttpGet]
		public ActionResult Version()
		{
			return new JsonResult
			{
				JsonRequestBehavior = JsonRequestBehavior.AllowGet,
				ContentEncoding = System.Text.Encoding.UTF8,
				Data = ProjectVersion
			};
		}

		[UrlRoute(Name = "Data", Path = "data")]
		[HttpGet]
		public ActionResult Data()
		{
			var projectsWithSonarDataAdded = from proj in TeamCityDataService.GetActiveProjects()
											 select new Models.Project
											 {
												 Id = proj.Id,
												 Name = proj.Name,
												 BuildConfigs = proj.BuildConfigs,
												 SonarProjectKey = proj.SonarProjectKey,
												 Url = proj.Url,
												 LastBuildDate = proj.LastBuildDate,
												 IconUrl = proj.IconUrl,
												 Statistics = GetProjectStatistics(proj),
												 CoverageGraph = string.IsNullOrWhiteSpace(proj.SonarProjectKey) ? null : getProjectGraph(proj.SonarProjectKey)
											 };

			return new JsonResult
			{
				JsonRequestBehavior = JsonRequestBehavior.AllowGet,
				ContentEncoding = System.Text.Encoding.UTF8,
				Data = projectsWithSonarDataAdded
			};
		}

		private ICodeStatistics GetProjectStatistics(IProject proj)
		{
			if (!proj.BuildConfigs.Any(T => T.CurrentBuildIsSuccesfull))
			{
				return null;
			}
			var buildID = proj.BuildConfigs.FirstOrDefault(T => T.CurrentBuildIsSuccesfull)?.CurrentBuildID;
			return buildID == null || buildID <= 0 ? null : TeamCityDataService.GetBuildStatistics(buildID.Value);
		}

		private object[][] getProjectGraph(string sonarKey)
		{
			var data = SonarDataService.GetProjectCoverage(sonarKey);
			return (from kvp in data select new object[] { kvp.Key, kvp.Value }).ToArray();
		}

		[UrlRoute(Name = "projectGraphData", Path = "projectgraph")]
		[HttpGet]
		public ActionResult ProjectGraph(string sonarKey)
		{
			var data = SonarDataService.GetProjectCoverage(sonarKey);

			return new JsonResult
			{
				JsonRequestBehavior = JsonRequestBehavior.AllowGet,
				ContentEncoding = System.Text.Encoding.UTF8,
				Data = (from kvp in data select new object[] { kvp.Key, kvp.Value }).ToArray()
			};
		}

		[UrlRoute(Name = "Github push events", Path = "pushevents")]
		[HttpGet]
		public ActionResult PushEvents()
		{
			return new JsonResult
			{
				JsonRequestBehavior = JsonRequestBehavior.AllowGet,
				ContentEncoding = System.Text.Encoding.UTF8,
				Data = GithubDataService.GetRecentEvents()
			};
		}

		[UrlRoute(Name = "Home", Path = "")]
		[HttpGet]
		public ActionResult Index()
		{
			return View();
		}
	}
}
﻿using Alta.WebApi.Client;
using LiteDB;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TownCrier.Database;

namespace TownCrier.Services
{
	public class AltaAPI
	{
		const int Timeout = 40;

		public IHighLevelApiClient ApiClient { get; private set; }
		private LiteDatabase _database;
		private IConfiguration _config;
		private readonly Timer_Service _timer;

		SHA512 sha512 = new SHA512Managed();

		public AltaAPI(LiteDatabase liteDatabase, IConfiguration configuration, Timer_Service timer)
		{
			_database = liteDatabase;
			_config = configuration;
			_timer = timer;

			StartWithEndpoint(HighLevelApiClientFactory.ProductionEndpoint);

			_timer.OnClockInterval += _timer_OnClockInterval;
		}

		private async void _timer_OnClockInterval(object sender, IServiceProvider e)
		{
			var col = _database.GetCollection<TownResident>("Users");
			var users = col.FindAll();

			foreach(var x in users.Where(x=>x.altaIdentifier.HasValue))
			{
				var altauser = await ApiClient.UserClient.GetUserInfoAsync(x.altaIdentifier.Value);
				
				x.UpdateAltaCredentials(altauser);
				col.Update(x);
			}
		}

		public void StartWithEndpoint(string endpoint)
		{
			if (ApiClient != null)
			{
				Console.WriteLine("Already have an Api Client");
				return;
			}

			SetApiClientLogging();

			ApiClient = HighLevelApiClientFactory.CreateHighLevelClient(endpoint, Timeout);
		}

		void SetApiClientLogging()
		{
			//HighLevelApiClientFactory.SetLogging(new AltaLoggerFactory());
		}



		public void StartOffline(LoginCredentials credentials)
		{
			if (ApiClient != null)
			{
				Console.WriteLine("Already have an Api Client");
				return;
			}

			SetApiClientLogging();

			ApiClient = HighLevelApiClientFactory.CreateOfflineHighLevelClient(credentials);
		}

		public async Task EnsureLoggedIn()
		{
			if (!ApiClient.IsLoggedIn)
			{
				if (!File.Exists("account.txt"))
				{
					Console.WriteLine("`account.txt` expected next to be next to the .exe " +
						"with the contents `username|password` (for your Alta account)");
					Console.ReadLine();
					throw new Exception("No credentials provided");
				}

				string username;
				string password;

				try
				{
					string[] account = System.IO.File.ReadAllText("account.txt").Trim().Split('|');

					username = account[0].Trim();
					password = account[1].Trim();

					if (password.Length < 64)
					{
						password = HashString(password);

						Console.WriteLine("Detected a password in the account file." +
							" Replaced it with a hash for security reasons.");

						File.WriteAllText("account.txt", username + "|" + password);
					}
				}
				catch (Exception e)
				{
					Console.WriteLine("`account.txt` found, but failed reading the contents." +
						" Expected format: `username|password` (for your Alta account)");
					Console.ReadLine();
					throw new Exception("Invalid credential format");
				}

				try
				{
					await ApiClient.LoginAsync(username, password);
					Console.WriteLine($"Logged in as {username} \n");
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}
			}
		}

		string HashString(string text)
		{
			//return Convert.ToBase64String(sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
			return BitConverter.ToString(sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", String.Empty).ToLowerInvariant();
		}
	}
}
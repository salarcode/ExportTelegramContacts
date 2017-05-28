using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Contacts;
using TLSharp.Core;

namespace ExportTelegramContacts
{
	class Program
	{
		private static TelegramClient TClient;
		private static TLUser TUser;


		public static int ApiId
		{
			get
			{
				var idStr = System.Configuration.ConfigurationManager.AppSettings["api_id"];
				int id;
				int.TryParse(idStr, out id);

				return id;
			}
		}
		public static string ApiHash
		{
			get
			{
				return System.Configuration.ConfigurationManager.AppSettings["api_hash"] ?? "";
			}
		}

		static void Main(string[] args)
		{

			Console.WriteLine("***************************");
			Console.WriteLine("Welcome to Telegram Contact Exporter");
			Console.WriteLine("***************************");
			try
			{
				Console.Write("Connecting to Telegram servers...");
				TClient = new TelegramClient(ApiId, ApiHash);
				var connect = TClient.ConnectAsync();
				connect.Wait();
				Console.WriteLine("Connected");
			}
			catch (Exception ex)
			{
				Console.WriteLine();
				Console.WriteLine(ex.Message);
				Console.ReadKey(intercept: true);
				return;
			}

			char? WriteMenu()
			{
				if (!TClient.IsUserAuthorized())
				{
					Console.WriteLine("You are not authenticated, please authenticate first.");
				}

				Console.WriteLine();
				Console.WriteLine("***************************");
				Console.WriteLine("1: Authenticate");
				Console.WriteLine("2: Export");
				Console.WriteLine("Q: Quit");
				Console.WriteLine(" ");
				Console.Write("Please enter your choice: ");
				return Console.ReadLine()?.ToLower().FirstOrDefault();
			}

			while (true)
			{
				var userInput = WriteMenu();

				switch (userInput)
				{
					case 'q':
						return;

					case '1':
						CallAuthenicate().Wait();
						break;

					case '2':
						CallExportContacts().Wait();
						break;

					default:
						Console.Clear();
						Console.WriteLine("Invalid input!");

						break;
				}
			}
		}

		private static async Task CallExportContacts()
		{
			try
			{
				if (!TClient.IsUserAuthorized())
				{
					Console.WriteLine("You are not authenticated, please authenticate first.");
					return;
				}

				Console.WriteLine($"Reading contacts...");

				var contacts = (await TClient.GetContactsAsync()) as TLContacts;

				Console.WriteLine($"Number of contacts: {contacts.users.lists.Count}");

				var fileName = $"Exported-{DateTime.Now.ToString("yyyy-MM-dd HH-mm.ss")}.vcf";
				var fileNameWihContacts = $"Exported-WithPhoto-{DateTime.Now.ToString("yyyy-MM-dd HH-mm.ss")}.vcf";

				Console.Write($"Don't export contacts without phone? [y/n] ");
				var filterResult = Console.ReadLine() ?? "";
				var dontExport = filterResult == "" || filterResult.ToLower() == "y";

				Console.WriteLine($"Writing to: {fileName}");
				using (var file = File.Create(fileName))
				using (var stringWrite = new StreamWriter(file))
				{
					var savedCount = 0;
					foreach (var user in contacts.users.lists.OfType<TLUser>())
					{
						if (dontExport)
						{
							if (string.IsNullOrWhiteSpace(user.phone))
								continue;
						}

						//vCard Begin
						stringWrite.WriteLine("BEGIN:VCARD");
						stringWrite.WriteLine("VERSION:2.1");
						//Name
						stringWrite.WriteLine("N:" + user.last_name + ";" + user.first_name);
						//Full Name
						stringWrite.WriteLine("FN:" + user.first_name + " " +
											 /* nameMiddle + " " +*/ user.last_name);
						stringWrite.WriteLine("TEL;CELL:" + LocalizeIranMobilePhone(user.phone));
						 
						//vCard End
						stringWrite.WriteLine("END:VCARD");

						savedCount++;
					}
					Console.WriteLine($"Total number of contacts saved: {savedCount}");
					Console.WriteLine();
				}

				Console.Write($"Do you want to export contacts with images? [y=enter/n] ");
				var exportWithImagesResult = Console.ReadLine() ?? "";
				var exportWithImages = exportWithImagesResult == "" || exportWithImagesResult.ToLower() == "y";

				Console.Write($"Save small or big images? [s=small=enter/b=big] ");
				var saveSmallResult = Console.ReadLine() ?? "";
				var saveSmallImages = saveSmallResult == "" || saveSmallResult.ToLower() == "y";

				if (exportWithImages)
				{
					Console.WriteLine($"Writing to: {fileNameWihContacts}");
					using (var file = File.Create(fileNameWihContacts))
					using (var stringWrite = new StreamWriter(file))
					{


						var savedCount = 0;
						foreach (var user in contacts.users.lists.OfType<TLUser>())
						{
							if (dontExport)
							{
								if (string.IsNullOrWhiteSpace(user.phone))
									continue;
							}

							var userPhoto = user.photo as TLUserProfilePhoto;
							string userPhotoString = null;
							if (userPhoto != null)
							{
								var photo = userPhoto.photo_big as TLFileLocation;
								if(saveSmallImages)
									photo = userPhoto.photo_small as TLFileLocation;

								if (photo != null)
								{
									Console.Write($"Reading prfile image for: {user.first_name} {user.last_name} ...");
									var fileResult = await TClient.GetFile(new TLInputFileLocation()
									{
										local_id = photo.local_id,
										secret = photo.secret,
										volume_id = photo.volume_id
									},
										filePartSize: -1);

									var smallPhotoBytes = fileResult.bytes;

									userPhotoString = Convert.ToBase64String(smallPhotoBytes);

									Console.WriteLine("Done");
								}
							}


							//System.IO.StringWriter stringWrite = new System.IO.StringWriter();
							//create an htmltextwriter which uses the stringwriter

							//vCard Begin
							stringWrite.WriteLine("BEGIN:VCARD");
							stringWrite.WriteLine("VERSION:2.1");
							//Name
							stringWrite.WriteLine("N:" + user.last_name + ";" + user.first_name);
							//Full Name
							stringWrite.WriteLine("FN:" + user.first_name + " " +
												  /* nameMiddle + " " +*/ user.last_name);
							stringWrite.WriteLine("TEL;CELL:" + LocalizeIranMobilePhone(user.phone));

							if (userPhotoString != null)
							{
								stringWrite.WriteLine("PHOTO;ENCODING=BASE64;TYPE=JPEG:");
								stringWrite.WriteLine(userPhotoString);
								stringWrite.WriteLine(string.Empty);
							}

							 
							//vCard End
							stringWrite.WriteLine("END:VCARD");

							savedCount++;
						}
						Console.WriteLine($"Total number of contacts with images saved: {savedCount}");
						Console.WriteLine();

					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return;
			}
		}

		/// <summary>
		/// +98935 - > 0935
		/// </summary>
		public static string LocalizeIranMobilePhone(string number)
		{
			if (string.IsNullOrEmpty(number))
				return number;
			if (number.StartsWith("+98"))
			{
				return number.Replace("+98", "0");
			}
			if (number.StartsWith("98"))
			{
				return "0" + number.Remove(0, 2);
			}
			return number;
		}

		private static async Task CallAuthenicate()
		{
			Console.Write("Please enter your mobile number: ");
			var phoneNumber = Console.ReadLine();

			string requestHash;
			try
			{
				requestHash = await TClient.SendCodeRequestAsync(phoneNumber);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.ReadKey(intercept: true);
				return;
			}
			Console.Write("Request is sent to your mobile, please enter the code here: ");
			var authCode = Console.ReadLine();

			try
			{
				TUser = await TClient.MakeAuthAsync(phoneNumber, requestHash, authCode);

				Console.WriteLine($"Authenicaion was successfull for Person Name:{TUser.first_name + " " + TUser.last_name}, Username={TUser.username}");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return;
			}
		}
	}
}

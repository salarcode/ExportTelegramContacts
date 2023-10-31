﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Account;
using TeleSharp.TL.Auth;
using TeleSharp.TL.Contacts;
using TLSharp.Core;
using TLSharp.Core.Exceptions;

namespace ExportTelegramContacts
{
	class Program
	{
		private static TelegramClient _client;
		private static TLUser _user;


		public static int ApiId
		{
			get
			{
				var idStr = System.Configuration.ConfigurationManager.AppSettings["api_id"];
				int.TryParse(idStr, out var id);

				return id;
			}
		}
		public static string ApiHash => System.Configuration.ConfigurationManager.AppSettings["api_hash"] ?? "";

		static void Main(string[] args)
		{
			Console.WriteLine("***************************");
			Console.WriteLine($"Welcome to Telegram Contacts Exporter Version {Assembly.GetExecutingAssembly().GetName().Version}");
			Console.WriteLine("***************************");
			try
			{
				var apiId = ApiId;
				var apiHash = ApiHash;

				if (string.IsNullOrWhiteSpace(apiHash) ||
				    apiHash.Contains("PLACEHOLDER") ||
				    apiId <= 0)
				{
					Console.WriteLine("The values for 'api_id' or 'api_hash' are NOT provided. Please enter these value in the '.config' file and try again.");
					Console.ReadKey(intercept: true);
					return;
				}


				Console.Write("Connecting to Telegram servers...");
				_client = new TelegramClient(ApiId, ApiHash);
				var connect = _client.ConnectAsync();
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
				if (!_client.IsUserAuthorized())
				{
					Console.WriteLine("You are not authenticated, please authenticate first.");
				}

				Console.WriteLine();
				Console.WriteLine("***************************");
				Console.WriteLine("1: Authenticate");
				Console.WriteLine("2: Export Contacts");
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
				if (!_client.IsUserAuthorized())
				{
					Console.WriteLine("You are not authenticated, please authenticate first.");
					return;
				}

				Console.WriteLine($"Reading contacts...");

				var contacts = (await _client.GetContactsAsync()) as TLContacts;

				Console.WriteLine($"Number of contacts: {contacts.Users.Count}");

				var fileName = $"ExportedContacts\\Exported-{DateTime.Now:yyyy-MM-dd HH-mm.ss}.vcf";
				var fileNameWihContacts = $"ExportedContacts\\Exported-WithPhoto-{DateTime.Now:yyyy-MM-dd HH-mm.ss}.vcf";

				Directory.CreateDirectory("ExportedContacts");

				Console.Write($"Export contacts without phone? [y/n] ");
				var filterResult = Console.ReadLine() ?? "";
				var dontExport = !(filterResult == "" || filterResult.ToLower() == "n");

				var usersList = contacts.Users.OfType<TLUser>().ToList();

				Console.WriteLine($"Writing to: {fileName}");
				using (var file = File.Create(fileName))
				using (var stringWrite = new StreamWriter(file))
				{
					var savedCount = 0;
					foreach (var user in usersList)
					{
						if (dontExport)
						{
							if (string.IsNullOrWhiteSpace(user.Phone))
								continue;
						}

						//vCard Begin
						stringWrite.WriteLine("BEGIN:VCARD");
						stringWrite.WriteLine("VERSION:2.1");
						//Name
						stringWrite.WriteLine("N:" + user.LastName + ";" + user.FirstName);
						//Full Name
						stringWrite.WriteLine("FN:" + user.FirstName + " " +
											 /* nameMiddle + " " +*/ user.LastName);
						stringWrite.WriteLine("TEL;CELL:" + ConvertFromTelegramPhoneNumber(user.Phone));

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

				if (exportWithImages)
				{
					Console.Write($"Save small or big images? [s=small=enter/b=big] ");
					var saveSmallResult = Console.ReadLine() ?? "";
					var saveSmallImages = saveSmallResult == "" || saveSmallResult.ToLower() == "s";

					Console.WriteLine($"Writing to: {fileNameWihContacts}");
					using (var file = File.Create(fileNameWihContacts))
					using (var stringWrite = new StreamWriter(file))
					{


						var savedCount = 0;
						foreach (var user in usersList)
						{
							if (dontExport)
							{
								if (string.IsNullOrWhiteSpace(user.Phone))
									continue;
							}

							string userPhotoString = null;
							try
							{
								var userPhoto = user.Photo as TLUserProfilePhoto;
								if (userPhoto != null)
								{
									var photo = userPhoto.PhotoBig as TLFileLocation;
									if (saveSmallImages)
										photo = userPhoto.PhotoSmall as TLFileLocation;

									if (photo != null)
									{
										var displayName = user.FirstName + " " + user.LastName;
										if (string.IsNullOrWhiteSpace(displayName))
											displayName = user.Username;

										Console.Write($"Reading prfile image for: {displayName}...");

										var smallPhotoBytes = await GetFile(_client,
											new TLInputFileLocation()
											{
												LocalId = photo.LocalId,
												Secret = photo.Secret,
												VolumeId = photo.VolumeId
											});

										// resize if it is the big image
										if (!saveSmallImages)
										{
											Console.Write("Resizing...");
											smallPhotoBytes = ResizeProfileImage(ref smallPhotoBytes);
										}

										userPhotoString = Convert.ToBase64String(smallPhotoBytes);

										Console.WriteLine("Done");
									}
								}
							}
							catch (Exception e)
							{
								Console.WriteLine("Failed due " + e.Message);
							}


							//System.IO.StringWriter stringWrite = new System.IO.StringWriter();
							//create an htmltextwriter which uses the stringwriter

							//vCard Begin
							stringWrite.WriteLine("BEGIN:VCARD");
							stringWrite.WriteLine("VERSION:2.1");
							//Name
							if (string.IsNullOrEmpty(user.LastName) && string.IsNullOrEmpty(user.FirstName))
								stringWrite.WriteLine("N:" + user.Username + ";");
							else
								stringWrite.WriteLine("N:" + user.LastName + ";" + user.FirstName);
							//Full Name
							stringWrite.WriteLine("FN:" + user.FirstName + " " +
												  /* nameMiddle + " " +*/ user.LastName);
							stringWrite.WriteLine("TEL;CELL:" + ConvertFromTelegramPhoneNumber(user.Phone));

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
				Console.WriteLine("Unknown error, if the error conitinues removing 'session.dat' file may help.\r\n" + ex.Message);
				return;
			}
		}

		private static async Task<byte[]> GetFile(TelegramClient client, TLInputFileLocation file)
		{
			int filePart = 512 * 1024;
			int offset = 0;
			using (var mem = new MemoryStream())
			{
				while (true)
				{
					if (!client.IsConnected)
					{
						await client.ConnectAsync(true);
					}
					var resFile = await client.GetFile(
						file,
						filePart, offset);

					mem.Write(resFile.Bytes, 0, resFile.Bytes.Length);
					offset += filePart;
					var readCount = resFile.Bytes.Length;

#if DEBUG
					Console.Write($" ... read {readCount} of {filePart} .");
#endif
					if (readCount < filePart)
						break;
				}
				return mem.ToArray();
			}
		}



		public static string ConvertFromTelegramPhoneNumber(string number)
		{
			if (string.IsNullOrEmpty(number))
				return number;
			if (number.StartsWith("0"))
				return number;
			if (number.StartsWith("+"))
				return number;
			return "+" + number;
		}


		private static async Task CallAuthenicate()
		{
			Console.Write("Please enter your mobile number (e.g: 14155552671): ");
			var phoneNumber = Console.ReadLine();

			string requestHash;
			try
			{
				requestHash = await _client.SendCodeRequestAsync(phoneNumber);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.ReadKey(intercept: true);
				return;
			}
			Console.Write("Request is sent to your mobile or the telegram app associated with this number, please enter the code here: ");
			var authCode = Console.ReadLine();
            TLUser user;
            try
			{
				_user = await _client.MakeAuthAsync(phoneNumber, requestHash, authCode);

				Console.WriteLine($"Authenicaion was successfull for Person Name:{_user.FirstName + " " + _user.LastName}, Username={_user.Username}");

#if DEBUG
				File.Copy("session.dat", "session.backup-copy.dat", true);
#endif

			}
            catch (CloudPasswordNeededException)
            {
                TLPassword passwordSetting = await _client.GetPasswordSetting();
                Console.WriteLine("This account needs cloud password.");

            TryAgain:
                Console.Write("Enter your password: ");
                string password = Console.ReadLine();

                try
                {
                    user = await _client.MakeAuthWithPasswordAsync( passwordSetting, password);
                }
                catch // If wrong password
                {
                    Console.WriteLine("Hint: " + passwordSetting.Hint);

                    if (passwordSetting.HasRecovery)
                    {
                        Console.WriteLine("Do you want to reset your password? [Y|N]");
                        string answer = Console.ReadLine();
                        if (answer == "Y")
                        {
                            Console.WriteLine("Recovery email: " + passwordSetting.EmailUnconfirmedPattern);

                            // Recover password
                            Console.Write("Enter email recovery code: ");
                            string recoveryCode = Console.ReadLine();
                            _client.SendRequestAsync<TLRequestRecoverPassword>(new TLRequestRecoverPassword() { Code = recoveryCode });
                        }
                    }
                    else
                    {
                        Console.WriteLine("This account doesn't have recovery!");
                    }

                    goto TryAgain;
                }
            }
            catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return;
			}
		}

		private static byte[] ResizeProfileImage(ref byte[] imageBytes)
		{
			int vcarImageSize = 300;
			int vcarImageQuality = 70;

			using (var imgMem = new MemoryStream(imageBytes))
			using (var img = Image.FromStream(imgMem))
			{
				using (var mediumImageStream = new MemoryStream())
				using (var mediumImage = ResizeImage(
					img,
					vcarImageSize,
					vcarImageSize))
				{
					var jpegCodec = JpegEncodingCodec;
					var jpegQuality = GetQualityParameter(vcarImageQuality);

					mediumImage.Save(mediumImageStream, jpegCodec, jpegQuality);

					// the new image should be smaller than the original one
					if (mediumImageStream.Length > imageBytes.Length)
					{
						return imageBytes;
					}
					else
					{
						return mediumImageStream.ToArray();
					}
				}
			}
		}


		private static Image ResizeImage(Image image, int maxWidth, int maxHeight)
		{
			var ratioX = (double)maxWidth / image.Width;
			var ratioY = (double)maxHeight / image.Height;
			var ratio = Math.Min(ratioX, ratioY);

			var newWidth = (int)(image.Width * ratio);
			var newHeight = (int)(image.Height * ratio);

			var newImage = new Bitmap(newWidth, newHeight);

			using (var graphics = Graphics.FromImage(newImage))
				graphics.DrawImage(image, 0, 0, newWidth, newHeight);

			return newImage;
		}


		private static EncoderParameters GetQualityParameter(int quality)
		{
			// Encoder parameter for image quality 
			var qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

			// JPEG image codec 
			var encoderParams = new EncoderParameters(1)
			{
				Param = { [0] = qualityParam }
			};

			return encoderParams;
		}

		private static ImageCodecInfo _jpegEncodingCodec;
		private static ImageCodecInfo JpegEncodingCodec => _jpegEncodingCodec ?? (_jpegEncodingCodec = GetEncoderInfo("image/jpeg"));

		/// <summary> 
		/// Returns the image codec with the given mime type 
		/// </summary> 
		private static ImageCodecInfo GetEncoderInfo(string mimeType)
		{
			// Get image codecs for all image formats 
			var codecs = ImageCodecInfo.GetImageEncoders();

			// Find the correct image codec 
			for (int i = 0; i < codecs.Length; i++)
				if (codecs[i].MimeType == mimeType)
					return codecs[i];

			return null;
		}
	}
}

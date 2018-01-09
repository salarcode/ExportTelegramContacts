using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
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
				if (!TClient.IsUserAuthorized())
				{
					Console.WriteLine("You are not authenticated, please authenticate first.");
					return;
				}

				Console.WriteLine($"Reading contacts...");

				var contacts = (await TClient.GetContactsAsync()) as TLContacts;

				Console.WriteLine($"Number of contacts: {contacts.Users.Count}");

				var fileName = $"ExportedContacts\\Exported-{DateTime.Now.ToString("yyyy-MM-dd HH-mm.ss")}.vcf";
				var fileNameWihContacts = $"ExportedContacts\\Exported-WithPhoto-{DateTime.Now.ToString("yyyy-MM-dd HH-mm.ss")}.vcf";

				Directory.CreateDirectory("ExportedContacts");

				Console.Write($"Export contacts without phone? [y/n] ");
				var filterResult = Console.ReadLine() ?? "";
				var dontExport = !(filterResult == "" || filterResult.ToLower() == "n");

				Console.WriteLine($"Writing to: {fileName}");
				using (var file = File.Create(fileName))
				using (var stringWrite = new StreamWriter(file))
				{
					var savedCount = 0;
					foreach (var user in contacts.Users.OfType<TLUser>())
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
						foreach (var user in contacts.Users.OfType<TLUser>())
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
										Console.Write($"Reading prfile image for: {user.FirstName} {user.LastName}...");
										
										var smallPhotoBytes = await GetFile(TClient,
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
				Console.WriteLine(ex.Message);
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

				Console.WriteLine($"Authenicaion was successfull for Person Name:{TUser.FirstName + " " + TUser.LastName}, Username={TUser.Username}");
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
		private static ImageCodecInfo JpegEncodingCodec
		{
			get { return _jpegEncodingCodec ?? (_jpegEncodingCodec = GetEncoderInfo("image/jpeg")); }
		}

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

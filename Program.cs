using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Octokit;
using System.Runtime.Remoting.Contexts;
using System.Reflection;

namespace Octokit_Testing
{
    internal class Program
    {
        private static Stopwatch SW = new Stopwatch();
        static async Task Main(string[] args)
        {

            var ProductInformation = new Octokit.ProductHeaderValue("Plugin");
            Console.WriteLine("Введите токен");
            string AccessToken = Console.ReadLine(); //access token
            string GithubAccount = "Georekon";
            string GithubRepo = "GeoAddin";

            var CredentialsToGithubClient = new Credentials(AccessToken);
            var client = new GitHubClient(ProductInformation)
            {
                Credentials = CredentialsToGithubClient
            };

            string PathToSave = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SaveFilesWithOctokit";
            if (!Directory.Exists(PathToSave))
            {
                Directory.CreateDirectory(PathToSave + "\\ArchiveFromOctokit");
                Directory.CreateDirectory(PathToSave + "\\OnlyDllFromGET");
                Directory.CreateDirectory(PathToSave + "\\ArchiveFromGET");
            }

            Repository repository = await client.Repository.Get(GithubAccount, GithubRepo); //интересующий репозиторий

            //вывод основной информации о репозитории
            Console.WriteLine("Repos: " + repository.Name + ", pushed at: " + repository.PushedAt); //выводим время последнего пуша в мастер
            Console.WriteLine(client.Repository.Commit.GetAll(repository.Id).Result.First().Commit.Committer.Name); //последний коммитер
            Console.WriteLine(client.Repository.Commit.GetAll(repository.Id).Result.First().Commit.Committer.Date); //время последнего коммита в мастер

            //вывод версии сборки репозитория
            using (StringReader reader = new StringReader(client.Repository.Content.GetAllContents(repository.Id, "GeoAddin/Properties/AssemblyInfo.cs").Result.First().Content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    //AssembInfo.Add(line);
                    if (line.Contains("AssemblyVersion") && !line.Contains("//"))
                        Console.WriteLine(line.Replace("[assembly: AssemblyVersion(\"", "").Replace("\")]", ""));
                }
            }
            ///*********************************************************************************************************///
            ///
            //скачивание архива через octokit
            SW.Start();
            var ArchiveContentFromOctokit = await client.Repository.Content.GetArchive(GithubAccount, "GeoAddin", ArchiveFormat.Zipball);
            File.WriteAllBytes($"{PathToSave}\\ArchiveFromOctokit\\{repository.Name}.zip", ArchiveContentFromOctokit);

            Console.WriteLine($"Архив сохранен через GetArchive за {SW.ElapsedMilliseconds} мс.");
            SW.Reset();
            ///*********************************************************************************************************///
            Console.WriteLine();
            //скачивание отдельных файлов из репозитория по фильтру
            SW.Start();
            var AllContentFromOctokit = client.Repository.Content.GetAllContents(repository.Id, "GeoAddin/bin/Debug").Result;
            foreach (var FileInRepo in AllContentFromOctokit)
            {
                if (Path.GetExtension(FileInRepo.Name) == ".dll")
                {
                    var URL = FileInRepo.DownloadUrl; //url для скачивания
                    var RequestToDownload = WebRequest.Create(URL);
                    RequestToDownload.Method = "GET";
                    var ResponseToDownload = RequestToDownload.GetResponse();
                    //Console.WriteLine($"Скачивание {FileInRepo.Name}...");
                    MemoryStream MemoryStreamToDownloadDLL = new MemoryStream(ResponseToDownload.ContentLength > 0 ? (int)ResponseToDownload.ContentLength : 20000);
                    using (Stream ResponseStream = ResponseToDownload.GetResponseStream())
                    {
                        ResponseStream.CopyTo(MemoryStreamToDownloadDLL);
                    }
                    byte[] DllBinary = MemoryStreamToDownloadDLL.ToArray();
                    MemoryStreamToDownloadDLL.Close();
                    File.WriteAllBytes($"{PathToSave}\\OnlyDllFromGET\\" + FileInRepo.Name, DllBinary);
                }
            }
            Console.WriteLine($"Сохранены отдельные файлы из репозитория за {SW.ElapsedMilliseconds} мс.");
            SW.Reset();
            ///*********************************************************************************************************///

            //скачивание архива посредством get-запроса
            SW.Start();
            var UrlToDownloadArchive = $"https://github.com/{GithubAccount}/{GithubRepo}/archive/refs/heads/master.zip"; //url для скачивания
            var RequestToDownloadArchive = WebRequest.Create(UrlToDownloadArchive);
            RequestToDownloadArchive.Method = "GET";
            RequestToDownloadArchive.ContentType = "application/x-www-form-urlencoded";
            RequestToDownloadArchive.Headers["Authorization"] = $"token {AccessToken}"; //подсовываем в заголовок токен
            var ResponseToDownloadArchive = RequestToDownloadArchive.GetResponse();
            MemoryStream MemoryStreamWithArchive = new MemoryStream(ResponseToDownloadArchive.ContentLength > 0 ? (int)ResponseToDownloadArchive.ContentLength : 20000);
            using (Stream ResponseStream = ResponseToDownloadArchive.GetResponseStream())
            {
                ResponseStream.CopyTo(MemoryStreamWithArchive);
            }
            byte[] ArchiveBinary = MemoryStreamWithArchive.ToArray();
            MemoryStreamWithArchive.Close();
            File.WriteAllBytes($"{PathToSave}\\ArchiveFromGET\\{GithubRepo}.zip", ArchiveBinary);
            Console.WriteLine($"Архив сохранен через GET-запрос за {SW.ElapsedMilliseconds} мс.");
            SW.Stop();
            ///*********************************************************************************************************///

            Console.ReadLine();



        }
    }
}

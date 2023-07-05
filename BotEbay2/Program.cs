using HtmlAgilityPack;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading;
using Org.BouncyCastle.Utilities;
using System.Diagnostics;
using NLog;
using System.IO;
using System.Xml;

namespace BotEbay
{
    class Program
    {
        public static string connectionString = "server=EC2AMAZ-GD98T28;user=root;password=PASSWORD;database=sacjen;";
        public static int Lancements = 0;
        static void Main(string[] args)
        {

            try
            {
                string fileName = "NLog.config";
                string filePath = Path.GetFullPath(fileName);
                Console.WriteLine("Chemin absolu du fichier de déboguage : " + filePath);
                LogManager.Setup().LoadConfigurationFromFile(Path.GetFullPath("NLog.config"));
                //Lance le Time, qui exécutera les différentes méthodes toutes les 20 min
                Timer();
            }
            catch (Exception ex)
            {
                Logger.Error("Erreur lors de l'exécution du programme : " + ex.Message);
            }
        }



        public class Article
        {
            public string Nom { get; set; }
            public string Prix { get; set; }
            public string Temps { get; set; }
            public string Image { get; set; }
            public string URL { get; set; }
            public string Marque { get; set; }

            public Article(string nom, string prix, string temps, string image, string url, string marque)
            {
                Nom = nom;
                Prix = prix;
                Temps = temps;
                Image = image;
                URL = url;
                Marque = marque;
            }
        }


        static void Timer()
        {
            //Configuration du Timer
            DateTime now = DateTime.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                Lancements++;
                Logger.Info("Lancement : " + Lancements);
                //Lance les méthodes
                List<string> liens = RecupLiens();
                List<Article> articles = RecupEbay2(liens);
                ViderTable();
                EnvoieTable(articles);


                // Ecriture dans des fichiers de log et dans la console pour le débogage
                Console.WriteLine("Heure :" + now);
                Console.WriteLine(stopwatch.Elapsed);
                Logger.Info("Lancement : " + Lancements + " : terminé");
                Logger.Info("Temps depuis le lancement du programme : " + stopwatch.Elapsed);
                long memoryUsed = GC.GetTotalMemory(false);
                Logger.Info("Utilisation de la mémoire : {0} bytes", memoryUsed);




                // Attendre 20 minutes avant la prochaine exécution
                Thread.Sleep(20 * 60 * 1000); // 20 minutes en millisecondes
            }

        }

        public static List<string> RecupLiens()
        {
            Logger.Info("RecupLiens");
            List<HtmlNode> allLiens = new List<HtmlNode>();

            //Récupération des pages principales à scraper
            HtmlWeb web = new HtmlWeb();
            HtmlDocument document1 = web.Load("https://www.ebay.fr/sch/i.html?_dkr=1&iconV2Request=true&_blrs=recall_filtering&_ssn=sacamainencuir&_oac=1");
            HtmlDocument document2 = web.Load("https://www.ebay.fr/sch/i.html?_dkr=1&iconV2Request=true&_blrs=recall_filtering&_ssn=jennynini73&_oac=1");
            HtmlDocument document3 = web.Load("https://www.ebay.fr/sch/i.html?_dkr=1&iconV2Request=true&_blrs=recall_filtering&_ssn=francoisetbenoit&_oac=1");


            HtmlNodeCollection items1 = document1.DocumentNode.SelectNodes("//li[contains(@class, 's-item')]");
            HtmlNodeCollection items2 = document2.DocumentNode.SelectNodes("//li[contains(@class, 's-item')]");
            HtmlNodeCollection items3 = document3.DocumentNode.SelectNodes("//li[contains(@class, 's-item')]");

            allLiens.AddRange(items1);
            allLiens.AddRange(items2);
            allLiens.AddRange(items3);


            List<string> Liens = new List<string>();

            //Récupération des différents articles  

            foreach (HtmlNode lien in allLiens)
            {
                string itemUrl = lien.SelectSingleNode(".//a[contains(@class, 's-item__link')]")
                                     .Attributes["href"].Value;


                if (itemUrl.Length < 100)
                {

                    Liens.Add(itemUrl);
                }
            }

            return Liens;
        }
        public static List<Article> RecupEbay2(List<string> liens)
        {
            Logger.Info("RecupEbay2");
            List<Article> articles = new List<Article>();

            foreach (string lien in liens)
            {
                try
                {
                    HtmlWeb web = new HtmlWeb();
                    HtmlDocument document = web.Load(lien);
                    HtmlNodeCollection items = document.DocumentNode.SelectNodes("//div[contains(@id, 'CenterPanelInternal')]");
                    if (items == null)
                    {
                        continue;
                    }

                    foreach (HtmlNode item in items)
                    {
                        try
                        {
                            string itemUrl = lien;
                            string name = HttpUtility.HtmlDecode(item.SelectSingleNode(".//h1[@class='x-item-title__mainTitle']/span[contains(@class, 'ux-textspans ux-textspans--BOLD')]")?.InnerText);
                            string mainImageUrl = item.SelectSingleNode(".//img[contains(@loading, 'eager')]").Attributes["src"].Value;
                            string price = item.SelectSingleNode(".//span[contains(@itemprop, 'price')]").Attributes["content"].Value;
                            string timeLeft = item.SelectSingleNode(".//span[@class='ux-timer']/span[contains(@class, 'ux-timer__text')]")?.InnerText;

                            HtmlNodeCollection imgNodes = item.SelectNodes(".//img[contains(@class, 'ux-image-magnify__image--original') and contains(@style, 'max-width:500px;max-height:500px;')]");
                            List<string> imageUrls = new List<string>();

                            if (imgNodes != null)
                            {
                                foreach (HtmlNode imgNode in imgNodes)
                                {
                                    string imageUrl = imgNode.GetAttributeValue("data-src", "");
                                    imageUrls.Add(imageUrl);
                                }
                            }

                            // Convertir la liste en tableau de chaînes
                            string[] images = imageUrls.ToArray();

                            // Afficher les URL des images
                            foreach (string imgUrl in images)
                            {
                                Console.WriteLine(imgUrl);
                            }

                            string pattern = @"\""(.*?)\""";
                            Match match = Regex.Match(name, pattern);
                            string marque = match.Success ? match.Groups[1].Value : "Autre";

                            Article article = new Article(name, price, timeLeft, mainImageUrl, itemUrl, marque);
                            articles.Add(article);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Erreur sur l'article " + item + ": " + ex.Message);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Erreur sur le chargement de la page des articles : " + ex.Message);
                    continue;
                }
            }

            int countarticle = articles.Count;
            Console.WriteLine(countarticle + " articles ont bien été chargés");
            Logger.Info(countarticle + " articles ont bien été chargés");

            return articles;
        }



        public static void ViderTable()
        {
            Logger.Info("ViderTable");
            //Se connecte à la BDD et vide les tables 
            MySqlConnection connection = new MySqlConnection(connectionString);
            string query = "TRUNCATE TABLE sac";
            string query2 = "TRUNCATE TABLE marques";
            try
            {
                connection.Open();
                MySqlCommand command = new MySqlCommand(query, connection);
                MySqlCommand command2 = new MySqlCommand(query2, connection);
                command2.ExecuteNonQuery();
                command.ExecuteNonQuery();
                Console.WriteLine($"Les tables ont été vidées.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vidage des tables : " + ex.Message);
                Logger.Error($"Erreur lors de la vidage des tables : " + ex.Message);
            }
            finally { connection.Close(); }
        }

        public static void EnvoieTable(List<Article> articles)
        {
            Logger.Info("EnvoieTable");
            //Se connecte à la BDD
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();
                if (connection.State == ConnectionState.Open)
                {
                    Console.WriteLine("La connexion à la base de données a réussi.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la connexion à la base de données: " + ex.Message);
                Logger.Error($"Erreur lors de la connexion à la base de données: " + ex.Message);

            }
            finally
            {
                connection.Close();

            }

            connection.Open();

            //Ajoute les articles et les marques aux tables 
            string insertQuery = "INSERT INTO sac (Nom, Prix, Temps, Image, URL, Marque) VALUES (@nom, @prix, @temps, @image, @url, @marque)";
            string insertQuery2 = @"INSERT IGNORE INTO marques (Marque, compteur)
                        SELECT @marque, IFNULL(MAX(compteur), 0) + 1 FROM marques";
            foreach (Article article in articles)
            {
                MySqlCommand command = new MySqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@nom", article.Nom);
                command.Parameters.AddWithValue("@prix", article.Prix);
                command.Parameters.AddWithValue("@temps", article.Temps);
                command.Parameters.AddWithValue("@image", article.Image);
                command.Parameters.AddWithValue("@url", article.URL);
                command.Parameters.AddWithValue("@marque", article.Marque);
                command.ExecuteNonQuery();

                MySqlCommand command2 = new MySqlCommand(insertQuery2, connection);
                command2.Parameters.AddWithValue("@marque", article.Marque);
                command2.ExecuteNonQuery();


            }




            connection.Close();



        }







        //Déboguage
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();








    }



}



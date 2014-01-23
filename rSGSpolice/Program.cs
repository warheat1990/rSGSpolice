using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RedditSharp;
using System.Security.Authentication;
using System.Threading;
using System.IO;
using MySql.Data.MySqlClient;

namespace rSGSpolice
{
    class Program
    {
        static DateTime currdate, prevdate;
        static String urlcurr, urlprev, user, title, datediff, dateleft, diffH, diffM, diffS, leftH, leftM, leftS;
        static SGSUser SGS;
        static CreatedThing CRT;
        static Comment ct = null;
        static String connStr = "";
        static Reddit reddit = new Reddit();

        static void Main(string[] args)
        {
            Console.WriteLine("rSGSpolice b1.7");

            //var reddit = new Reddit();
            
            //GET REDDIT ACCOUNT INFO
            while (reddit.User == null)
            {
                Console.Write("Username: ");
                var username = Console.ReadLine();
                Console.Write("Password: ");
                var password = ReadPassword();

                if (username.ToLower() != "rsgspolice")
                {
                    return;
                }

                try
                {
                    Console.WriteLine("Loading..");
                    reddit.LogIn(username, password);
                }
                catch (AuthenticationException)
                {
                    Console.WriteLine("Incorrect login.");
                }
            }
            Console.WriteLine("Logged in.");

            //GET SUBREDDIT INFO
            String sr = "";
            while (sr == "")
            {
                Console.Write("Subreddit: ");
                sr = Console.ReadLine();
            }
            var subreddit = reddit.GetSubreddit(sr);

            //GET DATABASE INFO
            bool flag = false;
            while (flag == false)
            {
                try
                {
                    Console.Write("ServerName: ");
                    var servername = ReadPassword();
                    Console.Write("DBName: ");
                    var DBName = ReadPassword();
                    Console.Write("DBUser: ");
                    var DBUser = ReadPassword();
                    Console.Write("DBPassword: ");
                    var DBPassword = ReadPassword();

                    connStr = "server=" + servername + ";database=" + DBName + ";userid=" + DBUser + ";password=" + DBPassword + ";";

                    MySqlConnection con = null;
                    con = new MySqlConnection(connStr);
                    con.Open();
                    MySqlCommand cmd = new MySqlCommand("SELECT * FROM Users WHERE reddit = 'warheat1990'", con);
                    cmd.ExecuteNonQuery();
                    con.Close();
                    Console.WriteLine("Connected");
                    flag = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " " + ex.Message);
                    flag = false;
                }
            }

            while (true)
            {
                try
                {
                    var posts = subreddit.GetNew();
                    Console.WriteLine(DateTime.Now.ToString() + " " + "GET");

                    foreach (var post in posts.Take(10).Reverse())
                    {
                        checkPost(post);
                    }

                    Thread.Sleep(60000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DateTime.Now.ToString() + " " + ex.Message);
                    Thread.Sleep(30000);
                }
            }
        }

        public static string ReadPassword()
        {
            var passbits = new Stack<string>();
            //keep reading
            for (ConsoleKeyInfo cki = Console.ReadKey(true); cki.Key != ConsoleKey.Enter; cki = Console.ReadKey(true))
            {
                if (cki.Key == ConsoleKey.Backspace)
                {
                    //rollback the cursor and write a space so it looks backspaced to the user
                    if (passbits.Count != 0)
                    {
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        passbits.Pop();
                    }
                }
                else
                {
                    Console.Write("*");
                    passbits.Push(cki.KeyChar.ToString());
                }
            }
            string[] pass = passbits.ToArray();
            Array.Reverse(pass);
            Console.Write(Environment.NewLine);
            return string.Join(string.Empty, pass);
        }

        private static void checkPost(Post post)
        {
            //GetInformation
            CRT = post;
            currdate = CRT.Created; //Date when thread created
            user = post.AuthorName; //Submitter username
            title = post.Title; //Thread title
            urlcurr = post.Url; //Thread url

            if (post.ApprovedBy == null)
            {
                if (title.Substring(0, 4) == "[H] " && title.Contains(" [W] ")) //check if it was a trade thread
                {
                    //Get previous created date from database on current user
                    SGS = getdatafromDB("SELECT Reddit, Url, Date, Abuse FROM Users WHERE Reddit = '" + user + "'");

                    if (SGS.User == null)
                    {
                        executeDB("INSERT INTO Users (Reddit, Url, Date, Abuse) VALUES ('" + user + "', '" + urlcurr + "', '" + currdate.ToString("dd.MM.yyyy HH:mm:ss") + "', 0)");
                        Console.WriteLine(DateTime.Now.ToString() + " " + "INSERT " + user);
                    }

                    else
                    {
                        prevdate = SGS.Date;
                        urlprev = SGS.Url;

                        if (urlcurr != urlprev)
                        {
                            if (currdate > prevdate)
                            {
                                String banned_by = reddit.GetPost(urlprev).BannedBy;

                                if (currdate.Subtract(prevdate) < TimeSpan.FromMinutes(5))
                                {
                                    executeDB("UPDATE Users SET Url = '" + urlcurr + "', Date = '" + currdate.ToString("dd.MM.yyyy HH:mm:ss") + "' WHERE Reddit = '" + user + "'");
                                    Console.WriteLine(DateTime.Now.ToString() + " " + "RENEW: UNDER 5 MINUTES " + SGS.User);
                                }

                                else if (banned_by != null && banned_by.ToLower() != "rsgspolice")
                                {
                                    executeDB("UPDATE Users SET Url = '" + urlcurr + "', Date = '" + currdate.ToString("dd.MM.yyyy HH:mm:ss") + "' WHERE Reddit = '" + user + "'");
                                    Console.WriteLine(DateTime.Now.ToString() + " " + "RENEW: PREVIOUS THREAD REMOVED " + SGS.User);
                                }

                                else if (currdate.Subtract(prevdate) < TimeSpan.FromHours(23)) //changed to 23 hours
                                {
                                    //remove thread if lower than 23 hours

                                    diffH = Convert.ToString(currdate.Subtract(prevdate).Hours);
                                    diffM = Convert.ToString(currdate.Subtract(prevdate).Minutes);
                                    diffS = Convert.ToString(currdate.Subtract(prevdate).Seconds);

                                    leftS = Convert.ToString(60 - currdate.Subtract(prevdate).Seconds);
                                    if (Convert.ToInt16(leftS) != 0)
                                    {
                                        leftM = Convert.ToString(59 - currdate.Subtract(prevdate).Minutes);
                                    }
                                    else
                                    {
                                        leftM = Convert.ToString(60 - currdate.Subtract(prevdate).Minutes);
                                    }
                                    if (Convert.ToInt16(leftM) != 0)
                                    {
                                        leftH = Convert.ToString(22 - currdate.Subtract(prevdate).Hours);
                                    }
                                    else
                                    {
                                        leftH = Convert.ToString(23 - currdate.Subtract(prevdate).Hours);
                                    }

                                    var comment = post.Comment("Your thread has been removed.  \nYour previous thread was posted '" + diffH + " hours " + diffM + " minutes " + diffS + " seconds' ago.  \nYou are allowed to make a new thread in '" + leftH + " hours '" + leftM + " minutes " + leftS + " seconds'.  \nYour previous thread's URL : " + urlprev + "  \n**I'm a bot, if you find any bugs, please contact /u/warheat1990.**"); //comment on the thread
                                    comment.Distinguish(DistinguishType.Moderator); //distinguish the comment
                                    post.Remove(); //remove the thread

                                    executeDB("UPDATE Users SET Abuse = Abuse + 1 WHERE Reddit ='" + SGS.User + "'");
                                    Console.WriteLine(DateTime.Now.ToString() + " " + "ABUSE " + SGS.User);
                                }
                                else
                                {
                                    //Update new url and time if higher than 24 hours
                                    if (urlcurr != urlprev)
                                    {
                                        executeDB("UPDATE Users SET Url = '" + urlcurr + "', Date = '" + currdate.ToString("dd.MM.yyyy HH:mm:ss") + "' WHERE Reddit = '" + user + "'");
                                        Console.WriteLine(DateTime.Now.ToString() + " " + "UPDATE " + SGS.User);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void executeDB(String query)
        {
            MySqlConnection con = null;
            try
            {
                con = new MySqlConnection(connStr);
                con.Open(); //open the connection

                MySqlCommand cmd = new MySqlCommand(query, con);
                cmd.ExecuteNonQuery(); //Execute the command
            }
            catch (MySqlException err) //We will capture and display any MySql errors that will occur
            {
                Console.WriteLine(DateTime.Now.ToString() + " " + "Error: " + err.ToString());
            }
            finally
            {
                if (con != null)
                {
                    con.Close(); //safely close the connection
                }
            }
        }

        private static SGSUser getdatafromDB(String query)
        {
            MySqlConnection con = null;
            MySqlDataReader reader = null;
            SGSUser SGS = new SGSUser();

            try
            {
                con = new MySqlConnection(connStr);
                con.Open();

                MySqlCommand cmd = new MySqlCommand(query, con);
                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    /*reader.GetString(0) will get the value of the first column of the table myTable because we selected all columns using SELECT * (all); the first loop of the while loop is the first row; the next loop will be the second row and so on...*/
                    SGS.User = reader.GetString(0);
                    SGS.Url = reader.GetString(1);
                    SGS.Date = DateTime.ParseExact(reader.GetString(2),"dd.MM.yyyy HH:mm:ss", null);
                    SGS.Abuse = reader.GetInt16(3);
                }
            }
            catch (MySqlException err)
            {
                Console.WriteLine(DateTime.Now.ToString() + " " + "Error: " + err.ToString());
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
                if (con != null)
                {
                    con.Close(); //close the connection
                }
            }

            return SGS;
        }
    }

    class SGSUser
    {
        private String _user;
        private String _url;
        private DateTime _date;
        private Int16 _abuse;

        public String User
        {
            get { return _user; }
            set { _user = value; }
        }

        public String Url
        {
            get { return _url; }
            set { _url = value; }
        }

        public DateTime Date
        {
            get { return _date; }
            set { _date = value; }
        }

        public Int16 Abuse
        {
            get { return _abuse; }
            set { _abuse = value; }
        }
    }
}

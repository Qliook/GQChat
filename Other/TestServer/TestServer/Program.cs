﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Server;

namespace TestServer
{
    class Program
    {
        static TcpListener server = new TcpListener(IPAddress.Any, 908);
        static StreamWriter sw = new StreamWriter("Log.txt");
        //static NetworkStream networkStream;

        static void Main(string[] args)
        {
            Console.Title = "Server";
            WriteLine("Загрузка сервера...", ConsoleColor.Yellow);
            sw.AutoFlush = true;
            server.Start();

            Thread thread = new Thread(new ThreadStart(NewConnect));
            thread.IsBackground = true;
            thread.Start();

            WriteLine("Сервер запущен!", ConsoleColor.Green);
            Console.ReadLine();
            sw.Close();
            Environment.Exit(0);
        }

        static void UpdateMessages(string text, Data.ClientConnectOnly onlyClient)//Для общего чата
        {
            try
            {
                WriteLine($"Cообщение в общий чат: {onlyClient.Nick}:{text}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                WriteLine($"Ошибка в общем чате: {ex.Message}", ConsoleColor.Red);
            }
        }

        static void MessagesClient(object i)
        {
            Data.ClientConnectOnly onlyClient = (Data.ClientConnectOnly)i;
            byte[] buffer = new byte[1024];
            int messi;

            while (true)
            {
                Task.Delay(10).Wait();

                try
                {
                    //Пример: %MES:Hello! (Ник мы уже знаем)
                    messi = onlyClient.ClientSocket.Client.Receive(buffer);
                    string answer = Encoding.UTF8.GetString(buffer, 0, messi);


                    if (answer.Contains("%MES"))//Для общего чата
                    {
                        Match regex = Regex.Match(answer, "%MES:(.*)");
                        string messagesText = regex.Groups[1].Value;


                    }
                    else if (answer.Contains("%NCT"))//Новый чат
                    {

                    }
                    else if (answer.Contains("%MSE"))//Для отдельного чата
                    {

                    }
                }
                catch (Exception ex)
                {
                    onlyClient.ClientSocket.Close();
                    Data.ClientsOnlyData.Remove(onlyClient);
                    WriteLine($"Клиент вышел: {ex.Message}", ConsoleColor.Red);
                    return;
                }
            }
        }

        static void CheckNewConnect(object i)
        {
            byte[] buffer = new byte[1024];
            TcpClient client = (TcpClient)i;
            //NetworkStream networkClient = client.GetStream();

            //Task.Delay(30).Wait();//Ждём отправки сообщения
            //int messi = networkClient.Read(buffer, 0, buffer.Length);
            int messi = client.Client.Receive(buffer);

            try
            {
                //Провека проги
                if (Encoding.UTF8.GetString(buffer, 0, messi) != "TCPCHAT 1.0")
                {
                    WriteLine("Ошибка: Cтарый или другой клиент!", ConsoleColor.Red);
                    client.Close();
                    return;//Проверить! (Готово)
                }
            }
            catch
            {
                WriteLine("Ошибка! Клиент!", ConsoleColor.Red);
                client.Close();
                return;
            }

            //Команды

            while (true)
            {
                Task.Delay(10).Wait();
                try
                {

                linkCommand:

                    //messi = client.Receive(buffer);
                    messi = client.Client.Receive(buffer);
                    string answer = Encoding.UTF8.GetString(buffer, 0, messi);

                    if (answer.Contains("%REG"))//регистрация
                    {
                        //email

                        Match regex = Regex.Match(answer, "%REG:(.*):(.*):(.*)");//Антон!
                        string email = regex.Groups[1].Value;

                        //string email = answer.
                        //TODO: Сделать проверку email через подтверждение (Нужен smtp сервер)

                        //пароль

                        string passworld = regex.Groups[3].Value;//TODO: Нужен md5

                        //Nick

                        string nick = regex.Groups[5].Value;

                        //Проверка

                        bool checkNewAccount = Database.CheckClientEmail(email);

                        if (checkNewAccount)
                        {
                            client.Client.Send(Encoding.UTF8.GetBytes("%REG:Такой email уже используется"));
                            goto linkCommand;
                        }
                        else
                        {
                            Database.AccountAdd(email, passworld, nick, Database.GetLastIdAccount());
                            client.Client.Send(Encoding.UTF8.GetBytes("1"));

                            WriteLine($"Новый аккаунт! {email}, {passworld}", ConsoleColor.Green);
                            return;
                        }
                    }
                    else if (answer.Contains("%LOG"))//Вход
                    {
                        //email
                        answer = Encoding.UTF8.GetString(buffer, 0, messi);

                        Match regex = Regex.Match(answer, "%LOG:(.*):(.*)");//Антон!
                        string email = regex.Groups[1].Value;

                        //пароль

                        string passworld = regex.Groups[3].Value;

                        //Проверка email

                        bool checkClient = Database.CheckClientEmail(email);

                        if (!checkClient)
                        {
                            //networkClient.Write(Encoding.UTF8.GetBytes("0"), 0, buffer.Length);
                            client.Client.Send(Encoding.UTF8.GetBytes("%LOG:Неверный пароль или email"));// False
                            goto linkCommand;
                        }
                        else
                        {
                            //Проверка пароля

                            bool checkPassworld = Database.CheckClientPassworld(passworld);

                            if (!checkPassworld)
                            {
                                client.Client.Send(Encoding.UTF8.GetBytes("%LOG:Неверный пароль или email"));
                                goto linkCommand;
                            }
                            else
                            {
                                client.Client.Send(Encoding.UTF8.GetBytes("1"));

                                //Инцилизация!

                                Data.ClientConnectOnly onlyClient = new Data.ClientConnectOnly(client,
                                    Database.GetNickClient(email), email, passworld);
                                Data.ClientsOnlyData.Add(onlyClient);

                                Thread thread = new Thread(new ParameterizedThreadStart(MessagesClient));
                                thread.IsBackground = true;
                                thread.Start(onlyClient);
                                WriteLine($"Вход аккаунт! {email}, {passworld}", ConsoleColor.Green);

                                return;
                            }
                        }
                    }
                }
                catch
                {
                    client.Close();
                    WriteLine("Error: Клиент!", ConsoleColor.Red);
                    return;
                }
            }
        }

        static void NewConnect()
        {
            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    Thread thread = new Thread(new ParameterizedThreadStart(CheckNewConnect));
                    thread.Start(client);
                }
                catch { WriteLine("Ошибка клиента!", ConsoleColor.Red); }
            }
        }

        static void WriteLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
            sw.WriteLine($"{DateTime.Now}: {text}");
        }
    }
}
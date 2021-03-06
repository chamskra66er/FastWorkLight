﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using HtmlAgilityPack;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using Tulpep.NotificationWindow;
using FastWorkLight.Models;
using ClosedXML.Excel;

namespace FastWorkLight
{
    public partial class Form1 : Form
    {
        string urlAddress;
        int valueList;
        PopupNotifier notifier = null;
        List<Job> jb = new List<Job>();
        public Form1()
        {
            InitializeComponent();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.FlatAppearance.BorderSize = 0;
            button1.FlatStyle = FlatStyle.Flat;

            CheckValueBox(richTextBox1);
            switch (comboBox1.Text)
            {
                case "hh.ru":
                    StartDriverHH(comboBox1, textBox1, textBox2);
                    GetHtmlAsyncHH(urlAddress, richTextBox1, progressBar1);
                    break;
                case "gorodrabot.ru":
                    await StartDriverGR(comboBox1, textBox1, textBox2);
                    GetHtmlAsyncGR(urlAddress, richTextBox1, progressBar1);               
                    break;
                default:
                    break;
            }

        }
                                 

        private static void CheckValueBox(RichTextBox richTextBox)
        {
            if (richTextBox.Text.Length > 0)
            {
                DialogResult dialogResult;
                dialogResult = MessageBox.Show("Данные будут утеряны. Хотите сохранить?", "Сообщение...", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    TXT tx = new TXT();
                    tx.Create(richTextBox);
                }
                
            }

        }

        private string StartDriverHH(ComboBox url, TextBox work, TextBox city)
        {
            IWebDriver driver = new ChromeDriver();
            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl($"https://{url.Text}");

            IWebElement elementCity = driver.FindElement(By.CssSelector("button[data-qa='mainmenu_areaSwitcher']"));
            elementCity.Click();
            Thread.Sleep(2000);
            IWebElement elementInputCity = driver.FindElement(By.CssSelector("input[type='text']"));
            elementInputCity.SendKeys($"{city.Text}" + OpenQA.Selenium.Keys.Enter);

            Thread.Sleep(1500);
            IWebElement buttonCity = driver.FindElement(By.CssSelector("li[class='suggest__item suggest__item_delimiter_line Bloko-Suggest-Item']"));
            buttonCity.Click();

            IWebElement elementInput = driver
                .FindElement(By.CssSelector("input[data-qa='search-input']"));
            elementInput.SendKeys($"{work.Text}" + OpenQA.Selenium.Keys.Enter);

            //find last list
            IWebElement link;
            try
            {
                if (driver
                .FindElements(By.CssSelector("a[class='bloko-button HH-Pager-Control']")).Last() != null)
                {
                    link = driver
                .FindElements(By.CssSelector("a[class='bloko-button HH-Pager-Control']")).Last();
                    valueList = Convert.ToInt32(link.GetAttribute("data-page"));
                }
                else { valueList = 1; }
            }
            catch (InvalidOperationException) { }

            urlAddress = driver.Url;
            driver.Quit();
            return urlAddress;

        }

        private async Task StartDriverGR(ComboBox url, TextBox work, TextBox city)
        {
            IWebDriver driver = new ChromeDriver();
            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl($"https://{url.Text}");

            IWebElement elementCity = driver.FindElement(By.CssSelector("input[data-name-id='city_domain']"));
            elementCity.Click();
            elementCity.Clear();
            Thread.Sleep(1500);
            elementCity.SendKeys($"{city.Text}");

            IWebElement elementInput = driver.FindElement(By.CssSelector("input[name='q']"));
            elementInput.Click();
            elementInput.Clear();
            Thread.Sleep(1500);
            elementInput.SendKeys($"{work.Text}" + OpenQA.Selenium.Keys.Enter);

            ///find last list
            int index = 1;
            IWebElement link;
            try
            {
                if (driver
                .FindElements(By.CssSelector("div[class='content__block content__module']")).FirstOrDefault() != null)
                {
                    link = driver.FindElements(By.CssSelector("li[class='pager__item']")).Last();
                    urlAddress = driver.Url;

                    HttpClient httpClient = new HttpClient();

                    while (true)
                    {
                        var newUrl = urlAddress + $"&p={index}";
                        var htmlWrite = await httpClient.GetStringAsync(newUrl);
                        var doc = new HtmlAgilityPack.HtmlDocument();
                        doc.LoadHtml(htmlWrite);

                        if (doc.DocumentNode.Descendants("div")
                        .Where(x => x.GetAttributeValue("class", "").Equals("snippet__body")).Last() == null)
                        {
                            break;
                        }
                        index++;
                    }
                }
                else { valueList = 1; }
            }
            catch (InvalidOperationException) { }


            urlAddress = driver.Url;
            valueList = index - 1;
            driver.Quit();
        }

        private void GetHtmlAsyncHH(string url, RichTextBox item, ProgressBar bar)
        {
            new Thread(async () =>
            {
                int index = 1;
                for (int i = 0; i < valueList + 1; i++)
                {

                    HttpClient httpClient = new HttpClient();
                    string end;
                    if (valueList <= 1)
                    { end = ""; }
                    else { end = $"&page={i}"; }

                    var htmlWrite = await httpClient.GetStringAsync(url + end);

                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(htmlWrite);

                    var ItemList = doc.DocumentNode.Descendants("div")
                        .Where(x => x.GetAttributeValue("data-qa", "").Equals("vacancy-serp__results")).ToList();

                    var WorkList = ItemList[0].Descendants("div")
                        .Where(x => x.GetAttributeValue("data-qa", "").Equals("vacancy-serp__vacancy")).ToList();
                    Action action = () =>{ bar.Maximum = WorkList.Count(); bar.Visible = true; };
                    if (InvokeRequired) { Invoke(action); }
                    else { action(); }

                    int progressIndex = 1;
                    foreach (var prod in WorkList)
                    {
                        var entity = prod.Descendants("a")
                            .Where(x => x.GetAttributeValue("class", "").Equals("bloko-link HH-LinkModifier")).
                            FirstOrDefault().InnerText.Trim();

                        string pay = "з/п не указана";
                        try
                        {
                            if (prod.Descendants("span")
                                .Where(x => x.GetAttributeValue("data-qa", "").Equals("vacancy-serp__vacancy-compensation"))
                                .FirstOrDefault().InnerText.Trim() != null)
                            {
                                pay = prod.Descendants("span")
                                .Where(x => x.GetAttributeValue("data-qa", "").Equals("vacancy-serp__vacancy-compensation"))
                                .FirstOrDefault().InnerText.Trim();
                            }
                        }
                        catch (NullReferenceException e) { }
                        var manage = prod.Descendants("a")
                            .Where(x => x.GetAttributeValue("data-qa", "").Equals("vacancy-serp__vacancy-employer"))
                            .FirstOrDefault().InnerText.Trim();
                        Action action1 = () =>
                        {
                            item.Text += $"{index}.  {entity} :    {manage}    -    {pay}\n";
                            bar.Value = progressIndex;                           
                            jb.Add(new Job{
                                Entity = entity,
                                Manage = manage,
                                Pay = pay
                            });
                        };
                        if (InvokeRequired) { Invoke(action1); }
                        else { action1(); }

                        index++;
                        progressIndex++;
                    }

                    Action action2 = () =>
                    { bar.Value = 0; };
                    if (InvokeRequired) { Invoke(action2); }
                    else { action2(); }

                }
                Action action3 = () =>
                {
                    notifier = new PopupNotifier();
                    notifier.Image = Properties.Resources.Good_Shield;
                    notifier.ImageSize = new Size(96, 96);
                    notifier.TitleText = "FastWorkLight";
                    notifier.ContentText = "Поиск завершен!";
                    notifier.Popup();
                };
                if (InvokeRequired) { Invoke(action3); }
                else { action3(); }
            }).Start();
            bar.Visible = false;
        }

        private void GetHtmlAsyncGR(string url, RichTextBox item, ProgressBar bar)
        {
            new Thread(async () =>
            {
                               

                int index = 1;
                for (int i = 0; i < valueList; i++)
                {

                    HttpClient httpClient = new HttpClient();
                    string end;
                    if (valueList <= 1)
                    { end = ""; }
                    else { end = $"&p={i}"; }

                    var htmlWrite = await httpClient.GetStringAsync(url + end);

                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(htmlWrite);

                    var ItemList = doc.DocumentNode.Descendants("div")
                        .Where(x => x.GetAttributeValue("class", "").Equals("default-grid__content")).ToList();

                    var WorkList = ItemList[0].Descendants("div")
                        .Where(x => x.GetAttributeValue("class", "").Equals("result-list__snippet vacancy snippet")).ToList();
                    Action action = () =>
                    { bar.Maximum = WorkList.Count(); };
                    if (InvokeRequired) { Invoke(action); }
                    else { action(); }

                    int progressIndex = 1;
                    foreach (var prod in WorkList)
                    {
                        var entity = prod.Descendants("a")
                            .Where(x => x.GetAttributeValue("class", "").Equals("link an-vc")).
                            FirstOrDefault().InnerText.Trim();

                        var pay = prod.Descendants("span")
                                    .Where(x => x.GetAttributeValue("class", "").Equals("snippet__salary")).
                                    FirstOrDefault().InnerText.Replace("\n", "").Replace("\t", "");


                        var manage = prod.Descendants("span")
                            .Where(x => x.GetAttributeValue("class", "").Equals("snippet__meta-value"))
                            .FirstOrDefault().InnerText.Trim();
                        Action action1 = () =>
                        {
                            item.Text += $"{index}.  {entity} :    {manage}    -    {pay}\n";
                            bar.Value = progressIndex;
                            jb.Add(new Job
                            {
                                Entity = entity,
                                Manage = manage,
                                Pay = pay
                            });
                        };
                        if (InvokeRequired) { Invoke(action1); }
                        else { action1(); }

                        index++;
                        progressIndex++;
                    }
                    Action action2 = () =>
                    { bar.Value = 0; };
                    if (InvokeRequired) { Invoke(action2); }
                    else { action2(); }
                }

                Action action3 = () =>
                {
                    notifier = new PopupNotifier();
                    notifier.Image = Properties.Resources.Good_Shield;
                    notifier.ImageSize = new Size(96, 96);
                    notifier.TitleText = "FastWorkLight";
                    notifier.ContentText = "Поиск завершен!";
                    notifier.Popup();
                };
                if (InvokeRequired) { Invoke(action3); }
                else { action3(); }

                
            }).Start();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void txtToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckValueBox(richTextBox1);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            TextChange(textBox1);
        }
        private void TextChange(TextBox textBox)
        {
            string template = "[0-9,!,@,$,%,^,&,*,=,/,*,%,?]";
            if (Regex.IsMatch(textBox.Text, template))
            {
                MessageBox.Show("неверный ввод");
                textBox.Text = textBox.Text.Remove(textBox.Text.Length - 1);
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            TextChange(textBox2);
        }

        private void справкаToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form2 form2 = new Form2();
            form2.ShowDialog();
        }

        private void xlsxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Excel xl = new Excel();
            xl.CreateNewFile(jb, richTextBox1);
        }

        private void pdfToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PDF pd = new PDF();
            pd.Create(jb, textBox1, richTextBox1);
        }     

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (richTextBox1.Text.Length > 0)
            {
                DialogResult result =
                    MessageBox.Show("Данные будут утеряны. Хотите сохранить?", "Сообщение", MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                    saveFileDialog1.Filter = "txt files (*.txt)|*.txt|exel files (*.xlxs)|*.xlsx|pdf files (*.pdf)|*.pdf";
                    saveFileDialog1.FilterIndex = 1;
                    saveFileDialog1.RestoreDirectory = true;
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        switch (saveFileDialog1.FilterIndex)
                        {
                            case 1:
                                TXT tx = new TXT();
                                tx.Create(richTextBox1);
                                this.Close();
                                break;
                            case 2:
                                Excel xl = new Excel();
                                xl.CreateNewFile(jb, richTextBox1);
                                this.Close();
                                break;
                            case 3:
                                PDF pd = new PDF();
                                pd.Create(jb, textBox1, richTextBox1);
                                this.Close();
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    Application.Exit();
                }
            }
        }
    }  
}

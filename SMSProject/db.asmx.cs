using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity.Core.Objects;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Script.Services;
using System.Web.Services;
using HttpPostAttribute = System.Web.Http.HttpPostAttribute;

namespace SMSProject
{
    /// <summary>
    /// Summary description for db
    /// </summary>
    [WebService(Namespace = "http://cattleman-001-site1/SMSservice")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [System.Web.Script.Services.ScriptService]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService
    public class db : System.Web.Services.WebService
    {
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string EnableDisable()
        {
            using (DB_A4A060_csEntities db = new DB_A4A060_csEntities())
            {
                var functionQuery = "";
                var note = "";
                var sendSMS = Boolean.Parse(ConfigurationManager.AppSettings["sendSMS"]);
                sendSMS = !sendSMS;
                ConfigurationManager.AppSettings["sendSMS"] = sendSMS.ToString();
                if (sendSMS)
                {
                    Context.Response.Output.WriteLine("Disable SMS Service");
                    functionQuery = "Enable";
                    note = "Service Enabled at " + DateTime.Now.AddHours(3);
                }
                else
                {
                    Context.Response.Output.WriteLine("Enable SMS Service");
                    functionQuery = "Disable";
                    note = "Service Disabled at " + DateTime.Now.AddHours(3);
                }
                db.Logs.Add(new Log
                {
                    user_id = "Admin",
                    page = HttpContext.Current.Request.Url.AbsoluteUri,
                    function_query = functionQuery,
                    error = null,
                    note = note,
                    datestamp = DateTime.Now.AddHours(3),
                    recipient = null
                });
                db.SaveChanges();
                Context.Response.End();
            }
            return string.Empty;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string SendAlerts()
        {
            if (Boolean.Parse(ConfigurationManager.AppSettings["sendSMS"]))
            {
                using (DB_A4A060_csEntities db = new DB_A4A060_csEntities())
                {

                    db.Logs.Add(new Log
                    {
                        user_id = "Admin",
                        page = HttpContext.Current.Request.Url.AbsoluteUri,
                        function_query = "Start SendAlerts",
                        error = null,
                        note = "Service Started at " + DateTime.Now.AddHours(3),
                        datestamp = DateTime.Now.AddHours(3),
                        recipient = null
                    });
                    db.SaveChanges();

                    var rows = db.Z_AlertLogs.Join(db.FarmCows,
                                                   z_alerts => z_alerts.bolus_id,
                                                   farm_cows => farm_cows.Bolus_ID,
                                                   (z_alerts, farm_cows) => new { z_alerts, farm_cows })
                                                   .Join(db.AspNetUsers,
                                                   combined_entry => combined_entry.farm_cows.AspNetUser_ID,
                                                   asp_users => asp_users.Id,
                                                   (combined_entry, asp_users) => new
                                                   {
                                                       username = asp_users.UserName,
                                                       msg = combined_entry.z_alerts.message,
                                                       date = combined_entry.z_alerts.date_emailsent,
                                                       phoneNumber = asp_users.PhoneNumber
                                                   }).Distinct();
                    string lastMessage = "";
                    string lastRecipient = "";
                    int messagesSent = 0;
                    List<Log> logEntries = new List<Log>();
                    foreach (var row in rows)
                    {
                        if (DateTime.Parse(row.date.ToString()).CompareTo(DateTime.Now.AddMinutes(-30).AddHours(3)) >= 0 &&
                            DateTime.Parse(row.date.ToString()).CompareTo(DateTime.Now.AddHours(3)) <= 0 &&
                            (row.msg != lastMessage || row.phoneNumber != lastRecipient))
                        {
                            string message = row.msg.Replace(';', ',');

                            // Fill in these feilds.
                            string login = "";
                            string password = "";
                            string url = "http://api.smsfeedback.ru/messages/v2/send/?login=" + login + "&password=" + password + "&phone=%2B" + row.phoneNumber + "&text=" + message;

                            try
                            {
                                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                                request.Method = "GET";
                                var response = (HttpWebResponse)request.GetResponse();

                                if (response.StatusCode.ToString().Equals("OK"))
                                    messagesSent++;

                                logEntries.Add(new Log
                                {
                                    user_id = "Admin",
                                    page = HttpContext.Current.Request.Url.AbsoluteUri,
                                    function_query = "SendAlerts",
                                    error = response.StatusCode.ToString(),
                                    note = "message:\'" + row.msg + "\' has been sent",
                                    datestamp = DateTime.Now.AddHours(3),
                                    recipient = row.phoneNumber
                                });
                                lastMessage = row.msg;
                                lastRecipient = row.phoneNumber;
                            }
                            catch (global::System.Exception e)
                            {
                                logEntries.Add(new Log
                                {
                                    user_id = "Admin",
                                    page = HttpContext.Current.Request.Url.AbsoluteUri,
                                    function_query = "SendAlertsError",
                                    error = e.Message,
                                    note = "message:\'" + row.msg + "\' encountered an error while sending.",
                                    datestamp = DateTime.Now.AddHours(3),
                                    recipient = row.phoneNumber
                                });
                            }
                        }
                    }
                    foreach (Log logRow in logEntries)
                    {
                        db.Logs.Add(logRow);
                        db.SaveChanges();
                    }
                    Context.Response.Output.WriteLine(messagesSent + " alert(s) were sent at " + DateTime.Now.AddHours(3).ToString() + ".");
                    db.Logs.Add(new Log
                    {
                        user_id = "Admin",
                        page = HttpContext.Current.Request.Url.AbsoluteUri,
                        function_query = "End SendAlerts",
                        error = null,
                        note = "Service Finished at " + DateTime.Now.AddHours(3),
                        datestamp = DateTime.Now.AddHours(3),
                        recipient = null
                    });
                    db.SaveChanges();
                }
            } else
            {
                Context.Response.Output.WriteLine("SMS Service is currently disabled.");
            }
            Context.Response.End();
            return string.Empty;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetStats()
        {
            using (DB_A4A060_csEntities db = new DB_A4A060_csEntities())
            {
                var timeMinusThirtyMinutes = DateTime.Now.AddMinutes(-30).AddHours(3);
                var sevenDaysAgo = DateTime.Today.AddDays(-7);
                var thirtyDaysAgo = DateTime.Today.AddDays(-30);
                var lastHalfHour = db.Logs.Where(l => l.datestamp >= timeMinusThirtyMinutes && l.function_query == "SendAlerts" ).Select(l => l.id).Count();
                var lastDay = db.Logs.Where(l => l.datestamp >= DateTime.Today && l.function_query == "SendAlerts").Select(l => l.id).Count();
                var lastWeek = db.Logs.Where(l => l.datestamp >= sevenDaysAgo && l.function_query == "SendAlerts").Select(l => l.id).Count();
                var lastMonth = db.Logs.Where(l => l.datestamp >= thirtyDaysAgo && l.function_query == "SendAlerts").Select(l => l.id).Count();
                var allTime = db.Logs.Where(l => l.function_query == "SendAlerts").Select(l => l.id).Count();

                string stats = lastHalfHour.ToString() + ";" + lastDay.ToString() + ";" + lastWeek.ToString() + ";" + lastMonth.ToString() + ";" + allTime.ToString();
                Context.Response.Output.WriteLine(stats);
            }
            Context.Response.End();
            return string.Empty;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetUsers()
        {
            using (DB_A4A060_csEntities db = new DB_A4A060_csEntities())
            {
                string response = "";
                var users = db.AspNetUsers.Select(u => new { u.UserName, u.PhoneNumber });
                foreach (var user in users)
                {
                    response = response + user.UserName + " (" + user.PhoneNumber + ");";
                }
                Context.Response.Output.WriteLine(response);
            }
            Context.Response.End();
            return string.Empty;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string SetNewNumber(string NameAndNumber, string PhoneNumber)
        {
            using (DB_A4A060_csEntities db = new DB_A4A060_csEntities())
            {
                var username = NameAndNumber.Split(' ')[0];
                var user = db.AspNetUsers.SingleOrDefault(u => u.UserName == username);
                user.PhoneNumber = PhoneNumber;
                db.SaveChanges();
                string response = user.UserName + " (" + user.PhoneNumber + ")";
                Context.Response.Output.WriteLine(response);
            }
            Context.Response.End();
            return string.Empty;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetGridEntires()
        {
            using (DB_A4A060_csEntities db = new DB_A4A060_csEntities())
            {
                Context.Response.Clear();
                Context.Response.ContentType = "application/json; charset=utf-8";
                Context.Response.Output.Write("[");
                var entries = db.Logs.Where(l => l.function_query == "SendAlerts").Join(db.Z_AlertLogs,
                                                                                        logs => logs.note,
                                                                                        z_alerts => "message:\'" + z_alerts.message + "\' has been sent",
                                                                                        (logs, z_alerts) => new { logs, z_alerts })
                                                                                        .Join(db.FarmCows, join1 => join1.z_alerts.bolus_id,
                                                                                        farm_cows => farm_cows.Bolus_ID,
                                                                                        (join1, farm_cows) => new { join1, farm_cows })
                                                                                        .Join(db.AspNetUsers,
                                                                                        join2 => join2.farm_cows.AspNetUser_ID,
                                                                                        asp_users => asp_users.Id,
                                                                                        (join2, asp_users) => new { join2, asp_users})
                                                                                        .Join(db.Farms,
                                                                                        join3 => join3.join2.farm_cows.AspNetUser_ID,
                                                                                        farms => farms.AspNetUser_Id,
                                                                                        (join3, farms) => new {
                                                                                            farm = farms.Name,
                                                                                            ev = join3.join2.join1.z_alerts.@event,
                                                                                            msg = join3.join2.join1.z_alerts.message,
                                                                                            date = join3.join2.join1.logs.datestamp,
                                                                                            owner = farms.Owner,
                                                                                            number = join3.asp_users.PhoneNumber
                                                                                        }).Distinct();

                string response = "";
                foreach (var entry in entries)
                {
                    response = response + "{" +
                        "\"Farm\":\"" + entry.farm + 
                        "\",\"Event\":\"" + entry.ev +
                        "\",\"Message\":\"" + entry.msg +
                        "\",\"Date\":\"" + entry.date.ToString() +
                        "\",\"Recipient\":\"" + entry.owner +
                        "\",\"PhoneNumber\":\"" + entry.number + 
                        "\"},";
                    
                }
                response = response.Remove(response.Length - 1);
                Context.Response.Output.Write(response);
                Context.Response.Output.Write("]");
            }
            Context.Response.End();
            return string.Empty;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetLastCall()
        {
            using (DB_A4A060_csEntities db = new DB_A4A060_csEntities())
            {
                var lastCall = db.Logs.Where(l => l.function_query == "Start SendAlerts").OrderByDescending(l => l.datestamp).FirstOrDefault();
                var lastAlert = db.Logs.Where(l => l.function_query == "SendAlerts").OrderByDescending(l => l.datestamp).FirstOrDefault();

                string stats = lastCall.datestamp.ToString() + ";" + lastAlert.datestamp.ToString();
                Context.Response.Output.WriteLine(stats);
            }
            Context.Response.End();
            return string.Empty;
        }

    }
}

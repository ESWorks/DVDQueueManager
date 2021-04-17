using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using DVDOrders.Objects;

namespace DVDOrders.Services
{
    public static class EmailService
    {
        public static async Task<bool> SendMessage(string to, string from, string subject, string message, EmailRecipients recipients, TextBox log = null)
        {
            try
            {
                if (recipients == EmailRecipients.None) return true;

                using (var client = new SmtpClient(Settings.EmailServerSite, Settings.EmailServerPort))
                {
                    client.EnableSsl = Settings.EmailServerSsl;
                    client.Credentials = new NetworkCredential(Settings.EmailServerUsername, Settings.EmailServerPassword);

                    using var mailMessage = new MailMessage(from,Settings.EmailAddress)
                    {
                        IsBodyHtml = true, Subject = subject, Body = message
                    };

                    if (recipients == EmailRecipients.Everyone || recipients == EmailRecipients.Sender)
                    {
                        foreach (var email in to.Split(';'))
                        {
                            mailMessage.To.Add(email);
                        }
                    }

                    await client.SendMailAsync(mailMessage);
                }
                return true;
            }
            catch (Exception e)
            {
                if (log != null) log.Text = e.Message;
                return false;
            }
        }

        public static async Task<bool> SendMessage(EmailType type, QueueEntry entry, EmailRecipients recipients, TextBox log = null)
        {
            try
            {
                return await SendMessage(entry.EmailAddress??Settings.EmailAddress,Settings.EmailServerAddress, 
                    $"[{type}] {DateTime.Now} - DVD Order Queue - {new FileInfo(entry.Source).Name}",
                    $"See DVD Progress Page for current status.\r\nThis message may be delayed due to processing time, subject lists current time sent.\r\nSource:\t{entry.Source}\r\nStatus:\t{entry.Status}\r\nCurrent Time Elapsed:\t{entry.TimeSpan}\r\nRobot:\t{entry.Robot}\r\nLines:\r\n{entry.Line1}\r\n{entry.Line2}\r\n{entry.Line3}", recipients);
            }
            catch (Exception e)
            {
                if (log != null) log.Text = e.Message;
                return false;
            }
        }
    }
}

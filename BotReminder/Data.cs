using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace BotReminder
{
    public class Data
    {
        private DateTime dateTime;
        private string info;
        private string title;

        public DateTime DateTime { get => dateTime; set => dateTime = (value > DateTime.MinValue && value < DateTime.MaxValue) ? value : throw new ArgumentException("Invalid DateTime value");}
        public string Info { get => info; set => info = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Info cannot be empty or whitespace");}
        public string Title { get => title; set => title = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Title cannot be empty or whitespace");}

        public Data(DateTime dateTime, string info, string title) 
        {
            DateTime = dateTime;
            Info = info;
            Title = title;
        }
        public Data() { }
    }
}

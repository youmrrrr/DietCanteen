using System;
using System.Collections.Generic;

namespace DietCanteen
{
    public class DailyMenu
    {
        public DateTime Date { get; set; }
        public List<string> DishNames { get; set; } = new List<string>();
        public string Notes { get; set; }

        // Для отображения в интерфейсе
        public string DisplayDate => Date.ToString("dd.MM.yyyy (dddd)");
        public int DishesCount => DishNames.Count;
    }
}
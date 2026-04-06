using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietCanteen
{
    public class RecipeDetail
    {
        public string DishName { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public string Processing { get; set; }
        public decimal ProductCalories { get; set; }
        public decimal ProductProtein { get; set; }
        public decimal ProductFat { get; set; }
        public decimal ProductCarbs { get; set; }
        public decimal CaloriesTotal { get; set; }
        public decimal ProteinTotal { get; set; }
        public decimal FatTotal { get; set; }
        public decimal CarbsTotal { get; set; }
    }
}

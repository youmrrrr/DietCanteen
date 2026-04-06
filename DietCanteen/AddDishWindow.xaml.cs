using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DietCanteen
{
    public partial class AddDishWindow : Window
    {
        private List<Product> availableProducts;
        public ObservableCollection<Ingredient> NewIngredients { get; set; }

        public Dish NewDish { get; private set; }

        public AddDishWindow(List<Product> products)
        {
            InitializeComponent();
            availableProducts = products;
            NewIngredients = new ObservableCollection<Ingredient>();

            // Загружаем продукты в комбобокс
            cmbProductName.ItemsSource = availableProducts;

            // Устанавливаем первый продукт по умолчанию, если есть
            if (availableProducts.Count > 0)
            {
                cmbProductName.SelectedIndex = 0;
            }

            // Устанавливаем обработку по умолчанию
            cmbProcessing.SelectedIndex = 0;

            dgIngredients.ItemsSource = NewIngredients;
        }

        private void btnAddIngredient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем выбран ли продукт
                if (cmbProductName.SelectedItem == null)
                {
                    MessageBox.Show("Выберите продукт!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Получаем выбранный продукт
                Product selectedProduct = cmbProductName.SelectedItem as Product;

                if (selectedProduct == null)
                {
                    MessageBox.Show("Ошибка выбора продукта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем количество
                if (!decimal.TryParse(txtQuantity.Text, out decimal quantity) || quantity <= 0)
                {
                    MessageBox.Show("Введите корректное количество (больше 0)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Получаем обработку
                string processing = "Варка";
                if (cmbProcessing.SelectedItem != null)
                {
                    processing = (cmbProcessing.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Варка";
                }

                // Добавляем ингредиент
                NewIngredients.Add(new Ingredient
                {
                    ProductName = selectedProduct.Name,
                    Quantity = quantity,
                    Processing = processing
                });

                // Очищаем поле количества
                txtQuantity.Text = "100";

                // Обновляем таблицу
                dgIngredients.ItemsSource = null;
                dgIngredients.ItemsSource = NewIngredients;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления ингредиента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверка названия
                if (string.IsNullOrWhiteSpace(txtDishName.Text))
                {
                    MessageBox.Show("Введите название блюда!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка цены
                if (!decimal.TryParse(txtPrice.Text, out decimal price) || price <= 0)
                {
                    MessageBox.Show("Введите корректную цену!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка ингредиентов
                if (NewIngredients.Count == 0)
                {
                    MessageBox.Show("Добавьте хотя бы один ингредиент!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем новое блюдо
                NewDish = new Dish
                {
                    Name = txtDishName.Text.Trim(),
                    Price = price,
                    InMenu = chkInMenu.IsChecked ?? false,
                    Calories = 0,
                    Protein = 0,
                    Fat = 0,
                    Carbs = 0
                };

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
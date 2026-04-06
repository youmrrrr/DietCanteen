using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Diagnostics;

namespace DietCanteen
{
    public partial class MainWindow : Window
    {
        // Списки данных
        private List<Product> products = new List<Product>();
        private List<Dish> dishes = new List<Dish>();
        private Dictionary<string, List<Ingredient>> recipes = new Dictionary<string, List<Ingredient>>();

        // Для добавления блюда
        private ObservableCollection<Ingredient> newIngredients = new ObservableCollection<Ingredient>();

        // Для планирования меню
        private List<DailyMenu> dailyMenus = new List<DailyMenu>();
        private string menusFilePath;

        // Путь к XML файлу
        private string xmlFilePath;

        public MainWindow()
        {
            InitializeComponent();

            // Инициализация для вкладки "Добавление"
            dgNewIngredients.ItemsSource = newIngredients;

            // Инициализация для вкладки "Удаление"
            cmbDeleteProduct.SelectionChanged += CmbDeleteProduct_SelectionChanged;
            cmbDeleteDish.SelectionChanged += CmbDeleteDish_SelectionChanged;

            // Определяем путь к XML файлу
            xmlFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MenuData.xml");

            // Если файл не найден в папке Debug, ищем в корне проекта
            if (!File.Exists(xmlFilePath))
            {
                string projectPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName;
                xmlFilePath = System.IO.Path.Combine(projectPath, "MenuData.xml");
            }

            // Путь для файла с сохраненными меню
            menusFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MenusData.xml");
        }

        // Кнопка "Загрузить данные"
        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadXmlData();
                txtStatus.Text = $"XML загружен. Загружено {products.Count} продуктов, {dishes.Count} блюд.";
                btnShowDishes.IsEnabled = true;
                btnShowMenu.IsEnabled = true;
                btnReset.IsEnabled = true;
                btnLoad.IsEnabled = false;

                // Загружаем продукты в комбобоксы
                cmbProductName.ItemsSource = products;
                cmbDeleteProduct.ItemsSource = products;
                cmbDeleteDish.ItemsSource = dishes;

                // Загружаем доступные блюда для планирования меню
                if (dishes.Count > 0)
                {
                    lstAvailableDishes.ItemsSource = dishes.OrderBy(d => d.Name).ToList();
                }

                // Загружаем сохраненные меню
                LoadDailyMenus();

                if (products.Count > 0)
                {
                    cmbProductName.SelectedIndex = 0;
                    cmbDeleteProduct.SelectedIndex = 0;
                }
                if (dishes.Count > 0)
                {
                    cmbDeleteDish.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки XML: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Ошибка загрузки XML";
            }
        }

        // Загрузка данных из XML
        private void LoadXmlData()
        {
            if (!File.Exists(xmlFilePath))
            {
                throw new FileNotFoundException($"Файл {xmlFilePath} не найден");
            }
            XDocument doc = XDocument.Load(xmlFilePath);

            // Загрузка продуктов
            products = (from p in doc.Root.Element("Products").Elements("Product")
                        select new Product
                        {
                            Name = (string)p.Attribute("name"),
                            Calories = (decimal)p.Attribute("calories"),
                            Protein = (decimal)p.Attribute("protein"),
                            Fat = (decimal)p.Attribute("fat"),
                            Carbs = (decimal)p.Attribute("carbs")
                        }).ToList();

            // Создаем словарь продуктов для быстрого поиска
            var productDict = products.ToDictionary(p => p.Name, p => p);

            // Загрузка рецептов
            recipes.Clear();
            foreach (var recipeElem in doc.Root.Element("Recipes").Elements("Recipe"))
            {
                string dishName = (string)recipeElem.Attribute("dishName");
                var ingredients = new List<Ingredient>();

                foreach (var ingElem in recipeElem.Elements("Ingredient"))
                {
                    ingredients.Add(new Ingredient
                    {
                        ProductName = (string)ingElem.Attribute("productName"),
                        Quantity = (decimal)ingElem.Attribute("quantity"),
                        Processing = (string)ingElem.Attribute("processing")
                    });
                }
                recipes[dishName] = ingredients;
            }

            // Загрузка блюд и расчет КБЖУ
            dishes = (from d in doc.Root.Element("Dishes").Elements("Dish")
                      let dishName = (string)d.Attribute("name")
                      let ingredients = recipes.ContainsKey(dishName) ? recipes[dishName] : new List<Ingredient>()
                      select new Dish
                      {
                          Name = dishName,
                          Price = (decimal)d.Attribute("price"),
                          InMenu = (bool)d.Attribute("inMenu"),
                          Calories = ingredients.Sum(i =>
                              productDict.ContainsKey(i.ProductName)
                              ? productDict[i.ProductName].Calories * i.Quantity / 100
                              : 0),
                          Protein = ingredients.Sum(i =>
                              productDict.ContainsKey(i.ProductName)
                              ? productDict[i.ProductName].Protein * i.Quantity / 100
                              : 0),
                          Fat = ingredients.Sum(i =>
                              productDict.ContainsKey(i.ProductName)
                              ? productDict[i.ProductName].Fat * i.Quantity / 100
                              : 0),
                          Carbs = ingredients.Sum(i =>
                              productDict.ContainsKey(i.ProductName)
                              ? productDict[i.ProductName].Carbs * i.Quantity / 100
                              : 0)
                      }).ToList();
        }

        // Загрузка сохраненных меню из XML
        private void LoadDailyMenus()
        {
            if (File.Exists(menusFilePath))
            {
                try
                {
                    XDocument doc = XDocument.Load(menusFilePath);
                    dailyMenus.Clear();

                    foreach (var menuElem in doc.Root.Elements("DailyMenu"))
                    {
                        var dailyMenu = new DailyMenu
                        {
                            Date = DateTime.Parse((string)menuElem.Attribute("date")),
                            Notes = (string)menuElem.Attribute("notes") ?? ""
                        };

                        foreach (var dishElem in menuElem.Elements("Dish"))
                        {
                            dailyMenu.DishNames.Add((string)dishElem.Attribute("name"));
                        }

                        dailyMenus.Add(dailyMenu);
                    }

                    dgSavedMenus.ItemsSource = null;
                    dgSavedMenus.ItemsSource = dailyMenus.OrderByDescending(m => m.Date).ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки меню: {ex.Message}");
                }
            }
        }

        // Сохранение меню в XML
        private void SaveDailyMenus()
        {
            try
            {
                XDocument doc = new XDocument();
                XElement root = new XElement("DailyMenus");

                foreach (var menu in dailyMenus)
                {
                    XElement menuElement = new XElement("DailyMenu",
                        new XAttribute("date", menu.Date.ToString("yyyy-MM-dd")),
                        new XAttribute("notes", menu.Notes ?? ""));

                    foreach (var dishName in menu.DishNames)
                    {
                        menuElement.Add(new XElement("Dish", new XAttribute("name", dishName)));
                    }

                    root.Add(menuElement);
                }

                doc.Add(root);
                doc.Save(menusFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения меню: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Загрузка меню на выбранную дату
        private void LoadMenuForDate(DateTime date)
        {
            var menu = dailyMenus.FirstOrDefault(m => m.Date.Date == date.Date);

            if (menu != null)
            {
                var selectedDishes = dishes.Where(d => menu.DishNames.Contains(d.Name)).ToList();
                lstSelectedDishes.ItemsSource = selectedDishes;
                txtMenuNotes.Text = menu.Notes;
                UpdateMenuStatistics(selectedDishes);
            }
            else
            {
                lstSelectedDishes.ItemsSource = null;
                txtMenuNotes.Text = "";
                UpdateMenuStatistics(new List<Dish>());
            }
        }

        // Обновление статистики выбранного меню
        private void UpdateMenuStatistics(List<Dish> selectedDishes)
        {
            txtSelectedCount.Text = selectedDishes.Count.ToString();
            decimal totalCalories = selectedDishes.Sum(d => d.Calories);
            txtTotalCalories.Text = $"{totalCalories:F0} ккал";
        }

        // Обработчик выбора продукта для удаления
        private void CmbDeleteProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDeleteProduct.SelectedItem is Product selectedProduct)
            {
                // Находим блюда, которые используют этот продукт
                var affectedDishes = dishes.Where(d => recipes.ContainsKey(d.Name) &&
                    recipes[d.Name].Any(i => i.ProductName == selectedProduct.Name)).ToList();

                lstAffectedDishes.ItemsSource = affectedDishes.Select(d => d.Name).ToList();

                if (affectedDishes.Count > 0)
                {
                    txtStatus.Text = $"Продукт '{selectedProduct.Name}' используется в {affectedDishes.Count} блюдах";
                }
                else
                {
                    txtStatus.Text = $"Продукт '{selectedProduct.Name}' не используется ни в одном блюде";
                }
            }
        }

        // Обработчик выбора блюда для удаления
        private void CmbDeleteDish_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDeleteDish.SelectedItem is Dish selectedDish)
            {
                txtDishInfo.Text = $"Название: {selectedDish.Name}\n" +
                                  $"Цена: {selectedDish.Price} руб.\n" +
                                  $"Калорийность: {selectedDish.Calories:F1} ккал\n" +
                                  $"Белки: {selectedDish.Protein:F1} г\n" +
                                  $"Жиры: {selectedDish.Fat:F1} г\n" +
                                  $"Углеводы: {selectedDish.Carbs:F1} г\n" +
                                  $"В меню: {(selectedDish.InMenu ? "Да" : "Нет")}";
            }
        }

        // Кнопка "Удалить продукт"
        private void btnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbDeleteProduct.SelectedItem == null)
                {
                    MessageBox.Show("Выберите продукт для удаления!", "Предупреждение",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Product selectedProduct = cmbDeleteProduct.SelectedItem as Product;
                if (selectedProduct == null) return;

                // Находим блюда, которые используют этот продукт
                var affectedDishes = dishes.Where(d => recipes.ContainsKey(d.Name) &&
                    recipes[d.Name].Any(i => i.ProductName == selectedProduct.Name)).ToList();

                string message = $"Вы действительно хотите удалить продукт '{selectedProduct.Name}'?\n\n";
                if (affectedDishes.Count > 0)
                {
                    message += $"ВНИМАНИЕ! Этот продукт используется в следующих блюдах:\n";
                    foreach (var dish in affectedDishes)
                    {
                        message += $"• {dish.Name}\n";
                    }
                    message += $"\nВсе эти блюда также будут удалены!\n\nЭто действие нельзя отменить!";
                }
                else
                {
                    message += $"Продукт не используется ни в одном блюде.\n\nЭто действие нельзя отменить!";
                }

                MessageBoxResult result = MessageBox.Show(message, "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Удаляем все блюда, использующие этот продукт
                    foreach (var dish in affectedDishes)
                    {
                        dishes.Remove(dish);
                        recipes.Remove(dish.Name);
                    }

                    // Удаляем продукт
                    products.Remove(selectedProduct);

                    // Сохраняем изменения
                    SaveToXml();

                    // Обновляем все списки
                    RefreshAllLists();

                    txtStatus.Text = $"Продукт '{selectedProduct.Name}' и {affectedDishes.Count} блюд удалены!";

                    MessageBox.Show($"Продукт '{selectedProduct.Name}' успешно удален!\n" +
                                   $"Удалено блюд: {affectedDishes.Count}", "Успешно",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении продукта: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Кнопка "Удалить блюдо" на вкладке "Удаление"
        private void btnDeleteDishFromTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbDeleteDish.SelectedItem == null)
                {
                    MessageBox.Show("Выберите блюдо для удаления!", "Предупреждение",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Dish selectedDish = cmbDeleteDish.SelectedItem as Dish;
                if (selectedDish == null) return;

                MessageBoxResult result = MessageBox.Show(
                    $"Вы действительно хотите удалить блюдо '{selectedDish.Name}'?\n\nЭто действие нельзя отменить!",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Удаляем блюдо
                    dishes.Remove(selectedDish);
                    recipes.Remove(selectedDish.Name);

                    // Сохраняем изменения
                    SaveToXml();

                    // Обновляем все списки
                    RefreshAllLists();

                    txtStatus.Text = $"Блюдо '{selectedDish.Name}' успешно удалено!";

                    MessageBox.Show($"Блюдо '{selectedDish.Name}' успешно удалено!", "Успешно",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении блюда: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обновление всех списков в интерфейсе
        private void RefreshAllLists()
        {
            // Обновляем таблицу блюд
            dgDishes.ItemsSource = null;
            dgDishes.ItemsSource = dishes;
            UpdateStatistics(dishes);

            // Обновляем комбобоксы
            cmbProductName.ItemsSource = null;
            cmbProductName.ItemsSource = products;
            cmbDeleteProduct.ItemsSource = null;
            cmbDeleteProduct.ItemsSource = products;
            cmbDeleteDish.ItemsSource = null;
            cmbDeleteDish.ItemsSource = dishes;

            // Обновляем доступные блюда для планирования меню
            if (dishes.Count > 0)
            {
                lstAvailableDishes.ItemsSource = dishes.OrderBy(d => d.Name).ToList();
            }
            else
            {
                lstAvailableDishes.ItemsSource = null;
            }

            // Загружаем сохраненные меню
            LoadDailyMenus();

            // Очищаем списки зависимых блюд
            lstAffectedDishes.ItemsSource = null;
            txtDishInfo.Text = "Выберите блюдо для просмотра информации";

            if (products.Count > 0)
            {
                cmbProductName.SelectedIndex = 0;
                cmbDeleteProduct.SelectedIndex = 0;
            }
            if (dishes.Count > 0)
            {
                cmbDeleteDish.SelectedIndex = 0;
            }
        }

        // Кнопка "Добавить продукт"
        private void btnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверка названия
                if (string.IsNullOrWhiteSpace(txtProductName.Text))
                {
                    MessageBox.Show("Введите название продукта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка калорий
                if (!decimal.TryParse(txtProductCalories.Text, out decimal calories) || calories < 0)
                {
                    MessageBox.Show("Введите корректное количество калорий!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка белков
                if (!decimal.TryParse(txtProductProtein.Text, out decimal protein) || protein < 0)
                {
                    MessageBox.Show("Введите корректное количество белков!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка жиров
                if (!decimal.TryParse(txtProductFat.Text, out decimal fat) || fat < 0)
                {
                    MessageBox.Show("Введите корректное количество жиров!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка углеводов
                if (!decimal.TryParse(txtProductCarbs.Text, out decimal carbs) || carbs < 0)
                {
                    MessageBox.Show("Введите корректное количество углеводов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка на дубликат
                if (products.Any(p => p.Name.Equals(txtProductName.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Продукт с таким названием уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем новый продукт
                var newProduct = new Product
                {
                    Name = txtProductName.Text.Trim(),
                    Calories = calories,
                    Protein = protein,
                    Fat = fat,
                    Carbs = carbs
                };

                // Добавляем продукт в список
                products.Add(newProduct);

                // Сохраняем изменения в XML
                SaveToXml();

                // Очищаем форму продукта
                ClearProductForm();

                // Обновляем все списки
                RefreshAllLists();

                txtStatus.Text = $"Продукт '{newProduct.Name}' успешно добавлен!";

                MessageBox.Show($"Продукт '{newProduct.Name}' успешно добавлен!\n\n" +
                               $"Калорийность: {calories:F1} ккал\n" +
                               $"Белки: {protein:F1} г\n" +
                               $"Жиры: {fat:F1} г\n" +
                               $"Углеводы: {carbs:F1} г",
                               "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении продукта: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Очистка формы продукта
        private void ClearProductForm()
        {
            txtProductName.Text = "";
            txtProductCalories.Text = "";
            txtProductProtein.Text = "";
            txtProductFat.Text = "";
            txtProductCarbs.Text = "";
        }

        // Кнопка "Добавить ингредиент"
        private void btnAddIngredient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbProductName.SelectedItem == null)
                {
                    MessageBox.Show("Выберите продукт!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Product selectedProduct = cmbProductName.SelectedItem as Product;
                if (selectedProduct == null)
                {
                    MessageBox.Show("Ошибка выбора продукта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(txtQuantity.Text, out decimal quantity) || quantity <= 0)
                {
                    MessageBox.Show("Введите корректное количество (больше 0)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string processing = "Варка";
                if (cmbProcessing.SelectedItem != null)
                {
                    processing = (cmbProcessing.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Варка";
                }

                newIngredients.Add(new Ingredient
                {
                    ProductName = selectedProduct.Name,
                    Quantity = quantity,
                    Processing = processing
                });

                txtQuantity.Text = "100";

                dgNewIngredients.ItemsSource = null;
                dgNewIngredients.ItemsSource = newIngredients;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления ингредиента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Удаление ингредиента
        private void RemoveIngredient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var ingredient = button?.DataContext as Ingredient;
                if (ingredient != null)
                {
                    newIngredients.Remove(ingredient);
                    dgNewIngredients.ItemsSource = null;
                    dgNewIngredients.ItemsSource = newIngredients;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления ингредиента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Кнопка "Сохранить блюдо"
        private void btnSaveDish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtNewDishName.Text))
                {
                    MessageBox.Show("Введите название блюда!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(txtNewPrice.Text, out decimal price) || price <= 0)
                {
                    MessageBox.Show("Введите корректную цену!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (newIngredients.Count == 0)
                {
                    MessageBox.Show("Добавьте хотя бы один ингредиент!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var productDict = products.ToDictionary(p => p.Name, p => p);

                decimal calories = 0, protein = 0, fat = 0, carbs = 0;

                foreach (var ingredient in newIngredients)
                {
                    if (productDict.ContainsKey(ingredient.ProductName))
                    {
                        var product = productDict[ingredient.ProductName];
                        calories += product.Calories * ingredient.Quantity / 100;
                        protein += product.Protein * ingredient.Quantity / 100;
                        fat += product.Fat * ingredient.Quantity / 100;
                        carbs += product.Carbs * ingredient.Quantity / 100;
                    }
                }

                var newDish = new Dish
                {
                    Name = txtNewDishName.Text.Trim(),
                    Price = price,
                    InMenu = chkNewInMenu.IsChecked ?? false,
                    Calories = calories,
                    Protein = protein,
                    Fat = fat,
                    Carbs = carbs
                };

                dishes.Add(newDish);
                recipes[newDish.Name] = newIngredients.ToList();
                SaveToXml();
                RefreshAllLists();
                ClearDishForm();

                txtStatus.Text = $"Блюдо '{newDish.Name}' успешно добавлено!";

                MessageBox.Show($"Блюдо '{newDish.Name}' успешно добавлено!\n\n" +
                               $"Калорийность: {calories:F1} ккал\n" +
                               $"Белки: {protein:F1} г\n" +
                               $"Жиры: {fat:F1} г\n" +
                               $"Углеводы: {carbs:F1} г",
                               "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении блюда: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Очистка формы добавления блюда
        private void ClearDishForm()
        {
            txtNewDishName.Text = "";
            txtNewPrice.Text = "";
            chkNewInMenu.IsChecked = false;
            newIngredients.Clear();
            dgNewIngredients.ItemsSource = null;
            dgNewIngredients.ItemsSource = newIngredients;
            txtQuantity.Text = "100";
            if (products.Count > 0)
            {
                cmbProductName.SelectedIndex = 0;
            }
            cmbProcessing.SelectedIndex = 0;
        }

        private void btnClearDishForm_Click(object sender, RoutedEventArgs e)
        {
            ClearDishForm();
        }

        private void btnShowDishes_Click(object sender, RoutedEventArgs e)
        {
            dgDishes.ItemsSource = null;
            dgDishes.ItemsSource = dishes;
            UpdateStatistics(dishes);
            txtStatus.Text = $"Показано {dishes.Count} блюд";
        }

        private void btnShowMenu_Click(object sender, RoutedEventArgs e)
        {
            var menuDishes = dishes.Where(d => d.InMenu).ToList();
            dgDishes.ItemsSource = null;
            dgDishes.ItemsSource = menuDishes;
            UpdateStatistics(menuDishes);
            txtStatus.Text = $"Показано {menuDishes.Count} блюд из меню";
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            dgDishes.ItemsSource = null;
            dgIngredients.ItemsSource = null;
            txtSelectedDish.Text = "Выберите блюдо из списка";
            ClearNutritionInfo();
            txtStatus.Text = "Данные сброшены. Нажмите 'Загрузить данные' для новой загрузки.";

            btnLoad.IsEnabled = true;
            btnShowDishes.IsEnabled = false;
            btnShowMenu.IsEnabled = false;
            btnReset.IsEnabled = false;
        }

        private void dgDishes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgDishes.SelectedItem is Dish selectedDish)
            {
                ShowDishComposition(selectedDish);
                txtSelectedDish.Text = $"Блюдо: {selectedDish.Name}";
                txtCalories.Text = $"Калорийность: {selectedDish.Calories:F1} ккал";
                txtProtein.Text = $"Белки: {selectedDish.Protein:F1} г";
                txtFat.Text = $"Жиры: {selectedDish.Fat:F1} г";
                txtCarbs.Text = $"Углеводы: {selectedDish.Carbs:F1} г";
                txtStatus.Text = $"Выбрано блюдо: {selectedDish.Name}";
            }
        }

        private void ShowDishComposition(Dish dish)
        {
            if (recipes.ContainsKey(dish.Name))
            {
                var ingredients = recipes[dish.Name];
                var productDict = products.ToDictionary(p => p.Name, p => p);

                var composition = from ing in ingredients
                                  let product = productDict.ContainsKey(ing.ProductName) ? productDict[ing.ProductName] : null
                                  select new
                                  {
                                      Продукт = ing.ProductName,
                                      Количество = $"{ing.Quantity} г",
                                      Обработка = ing.Processing,
                                      Ккал = product != null ? $"{product.Calories * ing.Quantity / 100:F1}" : "-",
                                      Белки = product != null ? $"{product.Protein * ing.Quantity / 100:F1}" : "-",
                                      Жиры = product != null ? $"{product.Fat * ing.Quantity / 100:F1}" : "-",
                                      Углеводы = product != null ? $"{product.Carbs * ing.Quantity / 100:F1}" : "-"
                                  };

                dgIngredients.ItemsSource = null;
                dgIngredients.ItemsSource = composition.ToList();
            }
            else
            {
                dgIngredients.ItemsSource = null;
            }
        }

        private void UpdateStatistics(List<Dish> displayedDishes)
        {
            txtTotalDishes.Text = $"Всего блюд: {displayedDishes.Count}";
            txtMenuDishes.Text = $"В меню: {displayedDishes.Count(d => d.InMenu)}";
            if (displayedDishes.Any())
            {
                txtAvgCalories.Text = $"Средняя калорийность: {displayedDishes.Average(d => d.Calories):F0} ккал";
            }
            else
            {
                txtAvgCalories.Text = "Средняя калорийность: -";
            }
        }

        private void SaveToXml()
        {
            try
            {
                XDocument doc = new XDocument();
                XElement root = new XElement("DietCanteen");

                XElement productsElement = new XElement("Products");
                foreach (var product in products)
                {
                    productsElement.Add(new XElement("Product",
                        new XAttribute("name", product.Name),
                        new XAttribute("calories", product.Calories),
                        new XAttribute("protein", product.Protein),
                        new XAttribute("fat", product.Fat),
                        new XAttribute("carbs", product.Carbs)
                    ));
                }
                root.Add(productsElement);

                XElement dishesElement = new XElement("Dishes");
                foreach (var dish in dishes)
                {
                    dishesElement.Add(new XElement("Dish",
                        new XAttribute("name", dish.Name),
                        new XAttribute("price", dish.Price),
                        new XAttribute("inMenu", dish.InMenu)
                    ));
                }
                root.Add(dishesElement);

                XElement recipesElement = new XElement("Recipes");
                foreach (var recipe in recipes)
                {
                    XElement recipeElement = new XElement("Recipe",
                        new XAttribute("dishName", recipe.Key));

                    foreach (var ingredient in recipe.Value)
                    {
                        recipeElement.Add(new XElement("Ingredient",
                            new XAttribute("productName", ingredient.ProductName),
                            new XAttribute("quantity", ingredient.Quantity),
                            new XAttribute("processing", ingredient.Processing)
                        ));
                    }
                    recipesElement.Add(recipeElement);
                }
                root.Add(recipesElement);

                doc.Add(root);
                doc.Save(xmlFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения XML: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearNutritionInfo()
        {
            txtCalories.Text = "Калорийность: -";
            txtProtein.Text = "Белки: -";
            txtFat.Text = "Жиры: -";
            txtCarbs.Text = "Углеводы: -";
            txtSelectedDish.Text = "Выберите блюдо из списка";
            txtTotalDishes.Text = "Всего блюд: -";
            txtMenuDishes.Text = "В меню: -";
            txtAvgCalories.Text = "Средняя калорийность: -";
        }

        // Обработчики для вкладки планирования меню

        private void btnLoadDailyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (dpMenuDate.SelectedDate.HasValue)
            {
                LoadMenuForDate(dpMenuDate.SelectedDate.Value);
                txtStatus.Text = $"Загружено меню на {dpMenuDate.SelectedDate.Value:dd.MM.yyyy}";
            }
        }

        private void btnSaveDailyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!dpMenuDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите дату!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedDishes = lstSelectedDishes.ItemsSource as List<Dish>;
            if (selectedDishes == null || selectedDishes.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одно блюдо в меню!", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime date = dpMenuDate.SelectedDate.Value;
            var existingMenu = dailyMenus.FirstOrDefault(m => m.Date.Date == date.Date);

            if (existingMenu != null)
            {
                existingMenu.DishNames.Clear();
                existingMenu.DishNames.AddRange(selectedDishes.Select(d => d.Name));
                existingMenu.Notes = txtMenuNotes.Text;
            }
            else
            {
                var newMenu = new DailyMenu
                {
                    Date = date,
                    Notes = txtMenuNotes.Text
                };
                newMenu.DishNames.AddRange(selectedDishes.Select(d => d.Name));
                dailyMenus.Add(newMenu);
            }

            SaveDailyMenus();
            LoadDailyMenus();

            txtStatus.Text = $"Меню на {date:dd.MM.yyyy} сохранено!";
            MessageBox.Show($"Меню на {date:dd.MM.yyyy} успешно сохранено!", "Успешно",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnClearDailyMenu_Click(object sender, RoutedEventArgs e)
        {
            lstSelectedDishes.ItemsSource = null;
            txtMenuNotes.Text = "";
            UpdateMenuStatistics(new List<Dish>());
            txtStatus.Text = "Меню очищено";
        }

        private void btnAddToMenu_Click(object sender, RoutedEventArgs e)
        {
            var selectedDishes = lstAvailableDishes.SelectedItems.Cast<Dish>().ToList();
            var currentDishes = (lstSelectedDishes.ItemsSource as List<Dish>) ?? new List<Dish>();

            foreach (var dish in selectedDishes)
            {
                if (!currentDishes.Any(d => d.Name == dish.Name))
                {
                    currentDishes.Add(dish);
                }
            }

            lstSelectedDishes.ItemsSource = currentDishes.OrderBy(d => d.Name).ToList();
            UpdateMenuStatistics(currentDishes);
            txtStatus.Text = $"Добавлено {selectedDishes.Count} блюд";
        }

        private void btnRemoveFromMenu_Click(object sender, RoutedEventArgs e)
        {
            var selectedDishes = lstSelectedDishes.SelectedItems.Cast<Dish>().ToList();
            var currentDishes = (lstSelectedDishes.ItemsSource as List<Dish>) ?? new List<Dish>();

            foreach (var dish in selectedDishes)
            {
                currentDishes.Remove(dish);
            }

            lstSelectedDishes.ItemsSource = currentDishes.OrderBy(d => d.Name).ToList();
            UpdateMenuStatistics(currentDishes);
            txtStatus.Text = $"Удалено {selectedDishes.Count} блюд";
        }

        private void btnAddAllToMenu_Click(object sender, RoutedEventArgs e)
        {
            var availableDishes = lstAvailableDishes.ItemsSource as List<Dish>;
            if (availableDishes != null)
            {
                lstSelectedDishes.ItemsSource = availableDishes.OrderBy(d => d.Name).ToList();
                UpdateMenuStatistics(availableDishes);
                txtStatus.Text = $"Добавлены все блюда ({availableDishes.Count})";
            }
        }

        private void btnRemoveAllFromMenu_Click(object sender, RoutedEventArgs e)
        {
            lstSelectedDishes.ItemsSource = new List<Dish>();
            UpdateMenuStatistics(new List<Dish>());
            txtStatus.Text = "Меню очищено";
        }

        private void txtSearchDish_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = txtSearchDish.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                lstAvailableDishes.ItemsSource = dishes.OrderBy(d => d.Name).ToList();
            }
            else
            {
                var filtered = dishes.Where(d => d.Name.ToLower().Contains(searchText))
                                    .OrderBy(d => d.Name)
                                    .ToList();
                lstAvailableDishes.ItemsSource = filtered;
            }
        }

        private void dgSavedMenus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgSavedMenus.SelectedItem is DailyMenu selectedMenu)
            {
                dpMenuDate.SelectedDate = selectedMenu.Date;
                LoadMenuForDate(selectedMenu.Date);
                txtStatus.Text = $"Загружено меню на {selectedMenu.Date:dd.MM.yyyy}";
            }
        }

        private void btnDeleteSavedMenu_Click(object sender, RoutedEventArgs e)
        {
            if (dgSavedMenus.SelectedItem is DailyMenu selectedMenu)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Вы действительно хотите удалить меню на {selectedMenu.Date:dd.MM.yyyy}?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    dailyMenus.Remove(selectedMenu);
                    SaveDailyMenus();
                    LoadDailyMenus();

                    if (dpMenuDate.SelectedDate?.Date == selectedMenu.Date.Date)
                    {
                        btnClearDailyMenu_Click(sender, e);
                    }

                    txtStatus.Text = $"Меню на {selectedMenu.Date:dd.MM.yyyy} удалено";
                }
            }
            else
            {
                MessageBox.Show("Выберите меню для удаления!", "Предупреждение",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

       private void btnExportToPDF_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (lstSelectedDishes.ItemsSource is List<Dish> selectedDishes && selectedDishes.Any())
        {
            string dateStr = dpMenuDate.SelectedDate?.ToString("dd.MM.yyyy") ?? DateTime.Now.ToString("dd.MM.yyyy");
            string dayOfWeek = dpMenuDate.SelectedDate?.ToString("dddd", new System.Globalization.CultureInfo("ru-RU")) ?? "";

            // Диалог сохранения файла
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "PDF файлы (*.pdf)|*.pdf";
            saveDialog.DefaultExt = ".pdf";
            saveDialog.FileName = $"Меню_{dateStr.Replace(".", "")}";
            saveDialog.Title = "Сохранить меню в PDF";

            if (saveDialog.ShowDialog() == true)
            {
                // Создаем PDF документ
                using (PdfDocument document = new PdfDocument())
                {
                    document.Info.Title = $"Меню на {dateStr}";
                    document.Info.Author = "Диетическая столовая";
                    document.Info.Subject = "Ежедневное меню";

                    // Добавляем страницу
                    PdfPage page = document.AddPage();
                    page.Width = XUnit.FromMillimeter(210); // A4 ширина
                    page.Height = XUnit.FromMillimeter(297); // A4 высота

                    // Создаем XGraphics для первой страницы
                    XGraphics gfx = XGraphics.FromPdfPage(page);

                    // Настройки шрифтов
                    XFont titleFont = new XFont("Segoe UI", 18, XFontStyle.Bold);
                    XFont headerFont = new XFont("Segoe UI", 11, XFontStyle.Bold);
                    XFont normalFont = new XFont("Segoe UI", 10, XFontStyle.Regular);
                    XFont boldFont = new XFont("Segoe UI", 10, XFontStyle.Bold);

                    double yPosition = 20;
                    double leftMargin = 20;
                    double rightMargin = page.Width - 20;

                    // Заголовок
                    string title = $"МЕНЮ НА {dateStr} ({dayOfWeek})";
                    XSize titleSize = gfx.MeasureString(title, titleFont);
                    double titleX = (page.Width - titleSize.Width) / 2;
                    gfx.DrawString(title, titleFont, XBrushes.Black, titleX, yPosition);
                    yPosition += 30;

                    // Линия под заголовком
                    gfx.DrawLine(XPens.Black, leftMargin, yPosition, rightMargin, yPosition);
                    yPosition += 15;

                    // Таблица
                    double[] columnWidths = { 30, 180, 70, 70, 100 };
                    double[] columnX = { leftMargin, leftMargin + columnWidths[0],
                                 leftMargin + columnWidths[0] + columnWidths[1],
                                 leftMargin + columnWidths[0] + columnWidths[1] + columnWidths[2],
                                 leftMargin + columnWidths[0] + columnWidths[1] + columnWidths[2] + columnWidths[3] };

                    // Рисуем заголовки таблицы
                    double headerY = yPosition;
                    gfx.DrawRectangle(XBrushes.LightGray, leftMargin, headerY,
                                     columnWidths.Sum(), 25);

                    gfx.DrawString("№", headerFont, XBrushes.Black,
                                  columnX[0] + 10, headerY + 17);
                    gfx.DrawString("Блюдо", headerFont, XBrushes.Black,
                                  columnX[1] + 5, headerY + 17);
                    gfx.DrawString("Цена", headerFont, XBrushes.Black,
                                  columnX[2] + 5, headerY + 17);
                    gfx.DrawString("Калории", headerFont, XBrushes.Black,
                                  columnX[3] + 5, headerY + 17);
                    gfx.DrawString("КБЖУ (б/ж/у)", headerFont, XBrushes.Black,
                                  columnX[4] + 5, headerY + 17);

                    yPosition += 25;

                    // Рисуем линии сетки заголовков
                    for (int i = 0; i <= columnWidths.Length; i++)
                    {
                        double x = leftMargin + columnWidths.Take(i).Sum();
                        gfx.DrawLine(XPens.Black, x, headerY, x, headerY + 25);
                    }
                    gfx.DrawLine(XPens.Black, leftMargin, headerY + 25, rightMargin, headerY + 25);

                    // Данные
                    int index = 1;
                    decimal totalCalories = 0;
                    decimal totalPrice = 0;
                    double rowHeight = 22;
                    int currentPage = 1;

                    foreach (var dish in selectedDishes)
                    {
                        double rowY = yPosition;

                        // Проверяем, нужна ли новая страница
                        if (rowY + rowHeight > page.Height - 50)
                        {
                            // Закрываем текущий gfx
                            gfx.Dispose();
                            
                            // Добавляем новую страницу
                            page = document.AddPage();
                            page.Width = XUnit.FromMillimeter(210);
                            page.Height = XUnit.FromMillimeter(297);
                            
                            // Создаем новый XGraphics для новой страницы
                            gfx = XGraphics.FromPdfPage(page);
                            yPosition = 20;
                            rowY = yPosition;

                            // Перерисовываем заголовки на новой странице
                            headerY = yPosition;
                            gfx.DrawRectangle(XBrushes.LightGray, leftMargin, headerY,
                                             columnWidths.Sum(), 25);
                            gfx.DrawString("№", headerFont, XBrushes.Black,
                                          columnX[0] + 10, headerY + 17);
                            gfx.DrawString("Блюдо", headerFont, XBrushes.Black,
                                          columnX[1] + 5, headerY + 17);
                            gfx.DrawString("Цена", headerFont, XBrushes.Black,
                                          columnX[2] + 5, headerY + 17);
                            gfx.DrawString("Калории", headerFont, XBrushes.Black,
                                          columnX[3] + 5, headerY + 17);
                            gfx.DrawString("КБЖУ (б/ж/у)", headerFont, XBrushes.Black,
                                          columnX[4] + 5, headerY + 17);

                            for (int i = 0; i <= columnWidths.Length; i++)
                            {
                                double x = leftMargin + columnWidths.Take(i).Sum();
                                gfx.DrawLine(XPens.Black, x, headerY, x, headerY + 25);
                            }
                            gfx.DrawLine(XPens.Black, leftMargin, headerY + 25, rightMargin, headerY + 25);

                            yPosition += 25;
                            rowY = yPosition;
                            currentPage++;
                        }

                        // Рисуем строку
                        gfx.DrawString(index.ToString(), normalFont, XBrushes.Black,
                                      columnX[0] + 10, rowY + 15);
                        gfx.DrawString(dish.Name, normalFont, XBrushes.Black,
                                      columnX[1] + 5, rowY + 15);
                        gfx.DrawString($"{dish.Price:F2} руб.", normalFont, XBrushes.Black,
                                      columnX[2] + 5, rowY + 15);
                        gfx.DrawString($"{dish.Calories:F1} ккал", normalFont, XBrushes.Black,
                                      columnX[3] + 5, rowY + 15);
                        gfx.DrawString($"{dish.Protein:F1}/{dish.Fat:F1}/{dish.Carbs:F1}", normalFont, XBrushes.Black,
                                      columnX[4] + 5, rowY + 15);

                        // Рисуем линии
                        for (int i = 0; i <= columnWidths.Length; i++)
                        {
                            double x = leftMargin + columnWidths.Take(i).Sum();
                            gfx.DrawLine(XPens.Black, x, rowY, x, rowY + rowHeight);
                        }
                        gfx.DrawLine(XPens.Black, leftMargin, rowY + rowHeight, rightMargin, rowY + rowHeight);

                        totalCalories += dish.Calories;
                        totalPrice += dish.Price;
                        index++;
                        yPosition += rowHeight;
                    }

                    // Итоговая информация
                    yPosition += 15;
                    string totalText = $"ИТОГО: {selectedDishes.Count} блюд";
                    gfx.DrawString(totalText, boldFont, XBrushes.Black, leftMargin, yPosition);
                    yPosition += 20;

                    string priceText = $"Общая стоимость: {totalPrice:F2} руб.";
                    gfx.DrawString(priceText, boldFont, XBrushes.Black, leftMargin, yPosition);
                    yPosition += 20;

                    string caloriesText = $"Общая калорийность: {totalCalories:F0} ккал";
                    gfx.DrawString(caloriesText, boldFont, XBrushes.Black, leftMargin, yPosition);
                    yPosition += 25;

                    // Примечания
                    if (!string.IsNullOrEmpty(txtMenuNotes.Text))
                    {
                        gfx.DrawLine(XPens.Gray, leftMargin, yPosition, rightMargin, yPosition);
                        yPosition += 10;
                        gfx.DrawString($"Примечания: {txtMenuNotes.Text}", normalFont, XBrushes.Black,
                                      leftMargin, yPosition);
                    }

                    // Нижний колонтитул
                    double footerY = page.Height - 20;
                    string footerText = $"Страница {currentPage}";
                    XSize footerSize = gfx.MeasureString(footerText, normalFont);
                    gfx.DrawString(footerText, normalFont, XBrushes.Gray,
                                  page.Width - footerSize.Width - 20, footerY);

                    // Сохраняем документ
                    document.Save(saveDialog.FileName);
                    
                    // Закрываем gfx
                    gfx.Dispose();
                }

                // Спрашиваем, открыть ли файл
                MessageBoxResult result = MessageBox.Show(
                    $"PDF файл успешно сохранен!\n\n{saveDialog.FileName}\n\nОткрыть файл?",
                    "Экспорт в PDF завершен",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
            }
        }
        else
        {
            MessageBox.Show("Нет блюд для экспорта!", "Предупреждение",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка при создании PDF: {ex.Message}\n\n{ex.StackTrace}",
                      "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
        private void btnPrintMenu_Click(object sender, RoutedEventArgs e)
        {
            if (lstSelectedDishes.ItemsSource is List<Dish> selectedDishes && selectedDishes.Any())
            {
                string dateStr = dpMenuDate.SelectedDate?.ToString("dd.MM.yyyy") ?? DateTime.Now.ToString("dd.MM.yyyy");
                string dayOfWeek = dpMenuDate.SelectedDate?.ToString("dddd", new System.Globalization.CultureInfo("ru-RU")) ?? "";

                // Создаем RichTextBox для форматированного отображения с прокруткой
                System.Windows.Documents.FlowDocument document = new System.Windows.Documents.FlowDocument();
                document.PagePadding = new Thickness(50);
                document.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
                document.FontSize = 12;

                // Заголовок
                var title = new System.Windows.Documents.Paragraph();
                title.FontSize = 20;
                title.FontWeight = FontWeights.Bold;
                title.TextAlignment = System.Windows.TextAlignment.Center;
                title.Margin = new Thickness(0, 0, 0, 20);
                title.Inlines.Add(new System.Windows.Documents.Run($"МЕНЮ НА {dateStr} ({dayOfWeek})"));
                document.Blocks.Add(title);

                // Таблица для блюд
                var table = new System.Windows.Documents.Table();
                table.BorderBrush = System.Windows.Media.Brushes.Black;
                table.BorderThickness = new Thickness(1);
                table.CellSpacing = 0;

                // Заголовки столбцов
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(40) });
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(200) });
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(150) });

                var headerRow = new System.Windows.Documents.TableRow();
                headerRow.Background = System.Windows.Media.Brushes.LightGray;
                headerRow.FontWeight = FontWeights.Bold;

                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("№"))));
                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Блюдо"))));
                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Цена"))));
                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Калории"))));
                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("КБЖУ (б/ж/у)"))));

                table.RowGroups.Add(new System.Windows.Documents.TableRowGroup());
                table.RowGroups[0].Rows.Add(headerRow);

                int index = 1;
                decimal totalCalories = 0;
                decimal totalPrice = 0;

                foreach (var dish in selectedDishes)
                {
                    var row = new System.Windows.Documents.TableRow();

                    row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(index.ToString()))));
                    row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(dish.Name))));
                    row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{dish.Price:F2} руб."))));
                    row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{dish.Calories:F1} ккал"))));
                    row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{dish.Protein:F1}/{dish.Fat:F1}/{dish.Carbs:F1}"))));

                    table.RowGroups[0].Rows.Add(row);

                    totalCalories += dish.Calories;
                    totalPrice += dish.Price;
                    index++;
                }

                document.Blocks.Add(table);

                // Итоговая информация
                var totalParagraph = new System.Windows.Documents.Paragraph();
                totalParagraph.Margin = new Thickness(0, 20, 0, 0);
                totalParagraph.FontWeight = FontWeights.Bold;
                totalParagraph.Inlines.Add(new System.Windows.Documents.Run($"ИТОГО: {selectedDishes.Count} блюд\n"));
                totalParagraph.Inlines.Add(new System.Windows.Documents.Run($"Общая стоимость: {totalPrice:F2} руб.\n"));
                totalParagraph.Inlines.Add(new System.Windows.Documents.Run($"Общая калорийность: {totalCalories:F0} ккал\n"));
                document.Blocks.Add(totalParagraph);

                // Примечания
                if (!string.IsNullOrEmpty(txtMenuNotes.Text))
                {
                    var notesParagraph = new System.Windows.Documents.Paragraph();
                    notesParagraph.Margin = new Thickness(0, 10, 0, 0);
                    notesParagraph.FontStyle = System.Windows.FontStyles.Italic;
                    notesParagraph.Inlines.Add(new System.Windows.Documents.Run($"Примечания: {txtMenuNotes.Text}"));
                    document.Blocks.Add(notesParagraph);
                }

                // Создаем окно просмотра с прокруткой
                var printWindow = new Window
                {
                    Title = "Предпросмотр меню",
                    Width = 800,
                    Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowState = WindowState.Normal
                };

                var richTextBox = new System.Windows.Controls.RichTextBox();
                richTextBox.Document = document;
                richTextBox.IsReadOnly = true;
                richTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                richTextBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                richTextBox.Padding = new Thickness(10);

                // Кнопки управления
                var buttonPanel = new StackPanel();
                buttonPanel.Orientation = Orientation.Horizontal;
                buttonPanel.HorizontalAlignment = HorizontalAlignment.Right;
                buttonPanel.Margin = new Thickness(10);

                var printButton = new Button
                {
                    Content = "Печать",
                    Width = 100,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = System.Windows.Media.Brushes.LightBlue,
                    FontWeight = FontWeights.Bold
                };
                printButton.Click += (s, args) =>
                {
                    try
                    {
                        System.Windows.Controls.PrintDialog printDialog = new System.Windows.Controls.PrintDialog();
                        if (printDialog.ShowDialog() == true)
                        {
                            var printDocument = new System.Windows.Documents.FlowDocument();
                            var range = new System.Windows.Documents.TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);

                            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                            {
                                range.Save(ms, System.Windows.DataFormats.XamlPackage);
                                ms.Position = 0;
                                range.Load(ms, System.Windows.DataFormats.XamlPackage);
                            }

                            printDocument = richTextBox.Document;
                            printDocument.PageHeight = printDialog.PrintableAreaHeight;
                            printDocument.PageWidth = printDialog.PrintableAreaWidth;
                            printDocument.PagePadding = new Thickness(50);

                            printDialog.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)printDocument).DocumentPaginator, "Меню");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                var closeButton = new Button
                {
                    Content = "Закрыть",
                    Width = 100,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = System.Windows.Media.Brushes.LightCoral,
                    FontWeight = FontWeights.Bold
                };
                closeButton.Click += (s, args) => printWindow.Close();

                buttonPanel.Children.Add(printButton);
                buttonPanel.Children.Add(closeButton);

                var mainPanel = new DockPanel();
                DockPanel.SetDock(buttonPanel, Dock.Bottom);
                mainPanel.Children.Add(richTextBox);
                mainPanel.Children.Add(buttonPanel);

                printWindow.Content = mainPanel;
                printWindow.Owner = this;
                printWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Нет блюд для печати!", "Предупреждение",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
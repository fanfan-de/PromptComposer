using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace PromptComposer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //// 绑定到左侧和中间“使用区”
        //public ObservableCollection<Category> Categories { get; } = new();

        //private Category _selectedCategory;
        //public Category SelectedCategory
        //{
        //    get => _selectedCategory;
        //    set { _selectedCategory = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedCategoryDisplay)); }
        //}

        //public string SelectedCategoryDisplay => SelectedCategory == null
        //    ? "当前未选择对象"
        //    : $"当前对象：{SelectedCategory.Name}";


        // 绑定到左侧和中间“使用区”
        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<CategorySet> CategorySets { get; } = new();

        private CategorySet _selectedSet;
        public CategorySet SelectedSet
        {
            get => _selectedSet;
            set
            {
                if (_selectedSet != value)
                {
                    // 切换前，把当前 UI 的改动回写到旧方案
                    FlushCurrentSetFromUI();
                    _selectedSet = value;

                    OnPropertyChanged();
                    ApplySelectedSetToView(); // 切换方案时把该方案的 Categories 映射到 UI 的 Categories
                }

                SaveState();
            }
        }


        private void FlushCurrentSetFromUI()
        {
            if (SelectedSet == null) return;

            // 先把选中项名字写回，保证可持久化
            foreach (var c in Categories)
                c.SelectedOptionName = c.SelectedOption?.Name;

            // 用 UI 的集合覆盖当前方案的集合（保持简单、干净的引用关系）
            SelectedSet.Categories = new ObservableCollection<Category>(Categories);
        }

        private void ApplySelectedSetToView()
        {
            Categories.Clear();
            if (SelectedSet == null) return;

            foreach (var c in SelectedSet.Categories)
            {
                // 还原 SelectedOption
                if (!string.IsNullOrWhiteSpace(c.SelectedOptionName))
                {
                    c.SelectedOption = c.Options.FirstOrDefault(o =>
                        string.Equals(o.Name, c.SelectedOptionName, StringComparison.OrdinalIgnoreCase));
                }
                Categories.Add(c);
            }
            SelectedCategory = Categories.FirstOrDefault();
        }


        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PromptComposer");
        private static readonly string DataFile = Path.Combine(AppDir, "categories.json");

        private Category _selectedCategory;
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedCategoryDisplay)); }
        }

        public string SelectedCategoryDisplay => SelectedCategory == null
            ? "当前未选择对象"
            : $"当前对象：{SelectedCategory.Name}";

        private bool LoadState()
        {
            try
            {
                if (!File.Exists(DataFile)) return false;

                var json = File.ReadAllText(DataFile, Encoding.UTF8);

                AppState state = null;

                // 尝试按新结构读取
                try
                {
                    state = JsonSerializer.Deserialize<AppState>(json);
                }
                catch
                {
                    // 兼容：旧版本文件是 [Category] 数组，自动迁移到单一方案
                    var oldCats = JsonSerializer.Deserialize<ObservableCollection<Category>>(json);
                    if (oldCats != null)
                    {
                        state = new AppState();
                        state.Sets.Add(new CategorySet
                        {
                            Name = "默认",
                            Categories = oldCats
                        });
                        state.ActiveSetName = "默认";
                    }
                }

                if (state == null || state.Sets.Count == 0) return false;

                CategorySets.Clear();
                foreach (var set in state.Sets)
                    CategorySets.Add(set);

                // 找到当前方案
                SelectedSet = CategorySets.FirstOrDefault(s =>
                    string.Equals(s.Name, state.ActiveSetName, StringComparison.OrdinalIgnoreCase))
                    ?? CategorySets.FirstOrDefault();

                // 把当前方案映射到 UI
                ApplySelectedSetToView();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SaveState()
        {
            try
            {
                Directory.CreateDirectory(AppDir);

                // 先把当前 UI 的改动回写到当前方案
                FlushCurrentSetFromUI();

                // —— 关键：同步所有方案的 SelectedOptionName —— //
                foreach (var set in CategorySets)
                {
                    if (set?.Categories == null) continue;
                    foreach (var c in set.Categories)
                        c.SelectedOptionName = c.SelectedOption?.Name;
                }

                var state = new AppState
                {
                    Sets = new ObservableCollection<CategorySet>(CategorySets),
                    ActiveSetName = SelectedSet?.Name
                };

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DataFile, json, Encoding.UTF8);
            }
            catch
            {
                // 可加日志
            }
        }

        public MainWindow()
        {
            //InitializeComponent();
            //DataContext = this;

            //// 提供一些默认示例，方便开箱即用
            //var catLens = new Category("镜头",
            //    new[] { "固定镜头", "俯视镜头", "正面镜头" });
            //var catStyle = new Category("风格",
            //    new[] { "电影感", "赛博朋克", "复古胶片" });
            //var catLighting = new Category("光照",
            //    new[] { "自然光", "逆光", "昏暗氛围" });

            //Categories.Add(catLens);
            //Categories.Add(catStyle);
            //Categories.Add(catLighting);

            //SelectedCategory = catLens;


            //InitializeComponent();
            //DataContext = this;

            // 先尝试加载
            // 先尝试加载
            if (!LoadState())
            {
                var defaultSet = new CategorySet { Name = "默认" };
                defaultSet.Categories.Add(new Category("镜头", new[] { "固定镜头", "俯视镜头", "正面镜头" }));
                defaultSet.Categories.Add(new Category("风格", new[] { "电影感", "赛博朋克", "复古胶片" }));
                defaultSet.Categories.Add(new Category("光照", new[] { "自然光", "逆光", "昏暗氛围" }));
                CategorySets.Add(defaultSet);

                SelectedSet = defaultSet;      // 会同步到 UI 的 Categories
            }

            InitializeComponent();
            DataContext = this;

            this.Closing += (_, __) => SaveState();
        }

        #region UI 事件

        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            var name = TxtNewCategory.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入对象名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("对象已存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Categories.Add(new Category(name));
            TxtNewCategory.Clear();
        }

        private void BtnRemoveCategory_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show("请先在对象列表中选择一个对象。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"确定要删除对象“{SelectedCategory.Name}”吗？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var idx = Categories.IndexOf(SelectedCategory);
                Categories.Remove(SelectedCategory);
                SelectedCategory = Categories.Count == 0 ? null
                    : Categories[Math.Clamp(idx - 1, 0, Categories.Count - 1)];
            }
        }

        private void BtnMoveCategoryUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null) return;
            var i = Categories.IndexOf(SelectedCategory);
            if (i > 0)
            {
                Categories.Move(i, i - 1);
            }
        }

        private void BtnMoveCategoryDown_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null) return;
            var i = Categories.IndexOf(SelectedCategory);
            if (i >= 0 && i < Categories.Count - 1)
            {
                Categories.Move(i, i + 1);
            }
        }

        private void BtnAddOption_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show("请先选择要添加选项的对象。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var name = TxtNewOption.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入选项名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedCategory.Options.Any(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("该选项已存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedCategory.Options.Add(new OptionItem(name));
            TxtNewOption.Clear();
        }

        private void BtnRemoveOption_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show("请先选择对象。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (LstOptionsOfSelected.SelectedItem is OptionItem opt)
            {
                SelectedCategory.Options.Remove(opt);
            }
            else
            {
                MessageBox.Show("请在右侧选项列表中选择要删除的选项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnMoveOptionUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null || LstOptionsOfSelected.SelectedItem is not OptionItem opt) return;
            var i = SelectedCategory.Options.IndexOf(opt);
            if (i > 0) SelectedCategory.Options.Move(i, i - 1);
        }

        private void BtnMoveOptionDown_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null || LstOptionsOfSelected.SelectedItem is not OptionItem opt) return;
            var i = SelectedCategory.Options.IndexOf(opt);
            if (i >= 0 && i < SelectedCategory.Options.Count - 1)
                SelectedCategory.Options.Move(i, i + 1);
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var sep = TxtSeparator.Text ?? ", ";
            bool includeLabel = ChkIncludeCategoryLabel.IsChecked == true;

            var parts = Categories
                .Where(c => c.SelectedOption != null)
                .Select(c => includeLabel ? $"{c.Name}: {c.SelectedOption.Name}" : c.SelectedOption.Name)
                .ToList();

            TxtOutput.Text = string.Join(sep, parts);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtOutput.Text))
            {
                Clipboard.SetText(TxtOutput.Text);
                //MessageBox.Show("已复制到剪贴板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClearSelections_Click(object sender, RoutedEventArgs e)
        {
            foreach (var c in Categories)
                c.SelectedOption = null;
            TxtOutput.Clear();
        }

        private void BtnClearOneSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Category cat)
            {
                cat.SelectedOption = null;
            }
        }


        private void BtnAddSet_Click(object sender, RoutedEventArgs e)
        {
            var name = TxtNewSetName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入方案名称。");
                return;
            }
            if (CategorySets.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("该方案名称已存在。");
                return;
            }

            var newSet = new CategorySet { Name = name };
            CategorySets.Add(newSet);

            // 切换到新方案（UI 的 Categories 会随 SelectedSet 改变而刷新）
            SelectedSet = newSet;

            TxtNewSetName.Clear();
            SaveState(); // 可选：立即持久化
        }

        private void BtnRemoveSet_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSet == null)
            {
                MessageBox.Show("请先选择要删除的方案。");
                return;
            }
            if (CategorySets.Count <= 1)
            {
                MessageBox.Show("至少保留一个方案。");
                return;
            }

            if (MessageBox.Show($"确定删除方案“{SelectedSet.Name}”吗？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var idx = CategorySets.IndexOf(SelectedSet);
                CategorySets.Remove(SelectedSet);

                // 切到相邻方案
                if (CategorySets.Count > 0)
                {
                    var newIndex = Math.Clamp(idx - 1, 0, CategorySets.Count - 1);
                    SelectedSet = CategorySets[newIndex];
                }
                else
                {
                    SelectedSet = null;
                }

                SaveState();
            }
        }

        private void BtnRenameSet_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSet == null)
            {
                MessageBox.Show("请先选择要重命名的方案。");
                return;
            }

            var name = TxtRenameSetName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入新名称。");
                return;
            }
            if (CategorySets.Any(s => s != SelectedSet &&
                                      s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("该名称已被其它方案占用。");
                return;
            }

            SelectedSet.Name = name;
            // 刷新绑定（有些模板需要通知；如果你用的是纯 DisplayMemberPath，也可不调）
            OnPropertyChanged(nameof(CategorySets));

            TxtRenameSetName.Clear();
            SaveState();
        }


        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }

    public class Category : INotifyPropertyChanged
    {
        //private string _name;
        //private OptionItem _selectedOption;

        //public string Name
        //{
        //    get => _name;
        //    set { _name = value; OnPropertyChanged(); }
        //}

        //public ObservableCollection<OptionItem> Options { get; } = new();

        //public OptionItem SelectedOption
        //{
        //    get => _selectedOption;
        //    set { _selectedOption = value; OnPropertyChanged(); }
        //}

        //public Category(string name)
        //{
        //    Name = name;
        //}

        //public Category(string name, string[] options)
        //{
        //    Name = name;
        //    foreach (var o in options) Options.Add(new OptionItem(o));
        //}

        //public event PropertyChangedEventHandler PropertyChanged;
        //private void OnPropertyChanged([CallerMemberName] string name = null)
        //    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Category() { } // 反序列化需要

        private string _name;
        private OptionItem _selectedOption;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        // 允许 set，便于反序列化直接写入
        public ObservableCollection<OptionItem> Options { get; set; } = new();

        [JsonIgnore] // 不直接存 SelectedOption
        public OptionItem SelectedOption
        {
            get => _selectedOption;
            set { _selectedOption = value; OnPropertyChanged(); }
        }

        // 用于持久化选中项（按名称）
        public string SelectedOptionName { get; set; }

        public Category(string name) => Name = name;

        public Category(string name, string[] options)
        {
            Name = name;
            foreach (var o in options) Options.Add(new OptionItem(o));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class OptionItem
    {
        //public string Name { get; set; }
        //public OptionItem(string name) => Name = name;
        //public override string ToString() => Name;



        public OptionItem() { } // 反序列化需要
        public string Name { get; set; }
        public OptionItem(string name) => Name = name;
        public override string ToString() => Name;
    }

    public class CategorySet
    {
        public string Name { get; set; } = "未命名方案";
        public ObservableCollection<Category> Categories { get; set; } = new();
    }

    public class AppState
    {
        public ObservableCollection<CategorySet> Sets { get; set; } = new();
        public string ActiveSetName { get; set; } // 当前选择的方案名
    }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Drawing.Printing;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ATMML
{
	public partial class LandingPage : ContentControl
	{
		public LandingPage(MainView mainView)
		{
			InitializeComponent();
			_mainView = mainView;

			Eula.Visibility = (EulaSigned()) ? Visibility.Collapsed : Visibility.Visible;
			AcceptDecline.Visibility = (EulaSigned()) ? Visibility.Collapsed : Visibility.Visible;
			CloseEula.Visibility = (EulaSigned()) ? Visibility.Visible : Visibility.Collapsed;
			LandingButtons.Visibility = (EulaSigned()) ? Visibility.Visible : Visibility.Collapsed;
			BottomPanel.Visibility = (EulaSigned()) ? Visibility.Visible : Visibility.Collapsed;
		}

		MainView _mainView = null;

		private void Label1_MouseEnter(object sender, MouseEventArgs e)
		{
			AutoMLImage.Visibility = Visibility.Visible;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label1_MouseLeave(object sender, MouseEventArgs e)
		{
			AutoMLImage.Visibility = Visibility.Collapsed;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}
		private void Label2_MouseEnter(object sender, MouseEventArgs e)
		{
			//MaterialsImage.Visibility = Visibility.Visible;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label2_MouseLeave(object sender, MouseEventArgs e)
		{
			//MaterialsImage.Visibility = Visibility.Collapsed;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label3_MouseEnter(object sender, MouseEventArgs e)
		{
			IndustryImage.Visibility = Visibility.Visible;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label3_MouseLeave(object sender, MouseEventArgs e)
		{
			IndustryImage.Visibility = Visibility.Collapsed;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}
		private void Label4_MouseEnter(object sender, MouseEventArgs e)
		{
			ChartImage.Visibility = Visibility.Visible;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label4_MouseLeave(object sender, MouseEventArgs e)
		{
			ChartImage.Visibility = Visibility.Collapsed;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}
		private void Label5_MouseEnter(object sender, MouseEventArgs e)
		{
			ForecastImage.Visibility = Visibility.Visible;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label5_MouseLeave(object sender, MouseEventArgs e)
		{
			ForecastImage.Visibility = Visibility.Collapsed;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}
		private void Label6_MouseEnter(object sender, MouseEventArgs e)
		{
			CompetitorImage.Visibility = Visibility.Visible;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label6_MouseLeave(object sender, MouseEventArgs e)
		{
			CompetitorImage.Visibility = Visibility.Collapsed;
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Label7_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Label7_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void MarketMaps_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new MarketMonitor(_mainView);
		}

		private void FundamentalML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new PortfolioBuilder(_mainView);
		}

		private void AODView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new Charts(_mainView);
		}


		private void Server_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new Timing(_mainView);
		}
		private void OurView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new Charts(_mainView);
		}
		private void ML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new AutoMLView(_mainView);
		}
		private void Alert_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new Alerts(_mainView);
		}

		private void CloseEula_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Eula.Visibility = Visibility.Collapsed;
			LandingButtons.Visibility = Visibility.Visible;
		}

		private void ShowEula_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Eula.Visibility = Visibility.Visible;
			LandingButtons.Visibility = Visibility.Collapsed;
			AcceptDecline.Visibility = Visibility.Collapsed;
			CloseEula.Visibility = Visibility.Visible;
		}

		private void Agree_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var sw = new StreamWriter(MainView.GetDataFolder() + @"\eula");
			sw.WriteLine("OK");
			sw.Close();
			Eula.Visibility = Visibility.Collapsed;
			LandingButtons.Visibility = Visibility.Visible;
			BottomPanel.Visibility = Visibility.Visible;
		}

		private void PrintEula_MouseDown(object sender, MouseButtonEventArgs e)
		{
			EulaDoc.Foreground = Brushes.Black;
			DoThePrint(EulaDoc);
			EulaDoc.Foreground = Brushes.White;
		}

		private void DoThePrint(System.Windows.Documents.FlowDocument document)
		{
			System.IO.MemoryStream s = new System.IO.MemoryStream();
			TextRange source = new TextRange(document.ContentStart, document.ContentEnd);
			source.Save(s, DataFormats.Xaml);
			FlowDocument copy = new FlowDocument();
			TextRange dest = new TextRange(copy.ContentStart, copy.ContentEnd);
			dest.Load(s, DataFormats.Xaml);

			System.Printing.PrintDocumentImageableArea ia = null;

			//PageRangeSelection pageRangeSelection = new PageRangeSelection();
			//PageRange pageRange = new PageRange();
			System.Windows.Xps.XpsDocumentWriter docWriter = System.Printing.PrintQueue.CreateXpsDocumentWriter(ref ia); //, ref pageRangeSelection, ref pageRange);

			if (docWriter != null && ia != null)
			{
				DocumentPaginator paginator = ((IDocumentPaginatorSource)copy).DocumentPaginator;

				paginator.PageSize = new Size(ia.MediaSizeWidth, ia.MediaSizeHeight);
				Thickness t = new Thickness(72);  // copy.PagePadding;
				copy.PagePadding = new Thickness(
								 Math.Max(ia.OriginWidth, t.Left),
								 Math.Max(ia.OriginHeight, t.Top),
								 Math.Max(ia.MediaSizeWidth - (ia.OriginWidth + ia.ExtentWidth), t.Right),
								 Math.Max(ia.MediaSizeHeight - (ia.OriginHeight + ia.ExtentHeight), t.Bottom));

				copy.ColumnWidth = double.PositiveInfinity;

				docWriter.Write(paginator);
			}

		}

		private bool EulaSigned()
		{
			return true; // MainView.ExistsUserData(@"eula");
		}

		private void Disagree_MouseDown(object sender, MouseButtonEventArgs e)
		{
			//Application.Current.Shutdown();
		}

		private void BtnSettings_Click(object sender, RoutedEventArgs e)
		{
			var menu = new System.Windows.Controls.ContextMenu();

			var changePassword = new System.Windows.Controls.MenuItem { Header = "Change Password" };
			changePassword.Click += (_, _) =>
				new ATMML.Auth.ChangePasswordDialog { Owner = Window.GetWindow(this) }.ShowDialog();
			menu.Items.Add(changePassword);

			if (ATMML.Auth.AuthContext.Current.CanManageUsers)
			{
				var manageUsers = new System.Windows.Controls.MenuItem { Header = "User Management" };
				manageUsers.Click += (_, _) =>
					new ATMML.Auth.UserManagementWindow { Owner = Window.GetWindow(this) }.ShowDialog();
				menu.Items.Add(manageUsers);
			}

			menu.Items.Add(new System.Windows.Controls.Separator());

			var logout = new System.Windows.Controls.MenuItem { Header = "Logout" };
			logout.Click += (_, _) =>
			{
				var confirm = MessageBox.Show(
					"Are you sure you want to exit?",
					"Exit ATMML",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);
				if (confirm == MessageBoxResult.Yes)
				{
					ATMML.Auth.AuthContext.Current.Logout();
					Application.Current.Shutdown();
				}
			};
			menu.Items.Add(logout);

			menu.PlacementTarget = BtnSettings;
			menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			menu.IsOpen = true;
		}
	}
}
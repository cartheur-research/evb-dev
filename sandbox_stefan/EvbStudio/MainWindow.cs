using System;
using System.Drawing;
using System.Windows.Forms;

namespace EvbStudio
{
  public partial class MainWindow : Form
  {
    private CsEvb.Serial serial_;

    public MainWindow()
    {
      main_menu_ = new MainMenu();
      MenuItem file_menu = main_menu_.MenuItems.Add("&File");
      file_menu.MenuItems.Add(new MenuItem("-"));
      file_menu.MenuItems.Add(new MenuItem("&Exit", new EventHandler(this.ExitMenuItemClick), Shortcut.CtrlX));

      this.Menu = main_menu_;

      InitializeComponent();

      serial_ = CsEvb.Serial.Create("COM3");

    }

    private void ExitMenuItemClick(object sender, EventArgs e)
    {
      this.Close();
    }

    private void MainWindowLoad(object sender, EventArgs e)
    {

    }

    private void MainWindowPaint(object sender, PaintEventArgs e)
    {
      Graphics g = e.Graphics;
      g.DrawString("Hello World", fnt_, Brushes.Blue, new Point(100, 100));
    }

    private Font fnt_ = new Font("Arial", 10);
  }
}

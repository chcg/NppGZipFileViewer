using System;
using System.Linq;
using System.Windows.Forms;

namespace NppGZipFileViewer.Forms;
public partial class DecompressForm : Form
{
    public DecompressForm() => InitializeComponent();

    private void CompressForm_Load(object sender, EventArgs e)
    {
        lstCompressors.DataSource = Preferences.Default.EnumerateCompressions().ToArray();
        lstCompressors.DisplayMember = nameof(Settings.CompressionSettings.CompressionAlgorithm);
    }

    private void lstCompressors_SelectedIndexChanged(object sender, EventArgs e)
    {

    }

    private void button1_Click(object sender, EventArgs e)
    {

    }

    public Settings.CompressionSettings CompressionSettings => lstCompressors.SelectedItem as Settings.CompressionSettings;
}

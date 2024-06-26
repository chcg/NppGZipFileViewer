﻿using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace NppGZipFileViewer.Forms;

public partial class SettingsDialog : Form
{
    public SettingsDialog()
    {
        InitializeComponent();
        Icon = Icon.FromHandle(NppGZipFileViewer.Properties.Resources.gzip_filled64.GetHicon());
    }

    private Preferences preferences;

    public Preferences Preferences
    {// ToDo:
        get
        {
            preferences.DecompressAll = chk_DecompressAll.Checked;
            preferences.CompressionAlgorithms.Clear();

            foreach (object chkCompr in chkListBoxCompressionAlg.CheckedItems)
                preferences.CompressionAlgorithms.Add(chkCompr as string);

            return preferences;
        }
        set
        {
            // clone preferences
            preferences = Clone(value);
            chk_DecompressAll.Checked = value.DecompressAll;
            //lst_Suffixes.Items.AddRange(value.Extensions.ToArray());

            string[] checkedItems = value.CompressionAlgorithms.ToArray();
            string[] uncheckedItems = value.EnumerateCompressions().Select(alg => alg.CompressionAlgorithm).Where(name => !checkedItems.Contains(name)).ToArray();

            chkListBoxCompressionAlg.Items.AddRange(checkedItems);
            chkListBoxCompressionAlg.Items.AddRange(uncheckedItems);

            for (int i = 0; i < checkedItems.Length; i++)
                SetItemCheckState(i, CheckState.Checked);
            for (int i = 0; i < uncheckedItems.Length; i++)
                SetItemCheckState(i + checkedItems.Length, CheckState.Unchecked);
        }
    }

    private Preferences Clone(Preferences value)
    {
        // Since Preferences is serializable;
        XmlSerializer xmlSerializer = new(typeof(Preferences));
        using MemoryStream memoryStream = new();
        xmlSerializer.Serialize(memoryStream, value);
        _ = memoryStream.Seek(0, SeekOrigin.Begin);
        return xmlSerializer.Deserialize(memoryStream) as Preferences;
    }

    ~SettingsDialog()
    {
        Icon.Dispose();
    }

    private void SettingsDialog_Load(object sender, EventArgs e)
    {
        toolTip1.SetToolTip(chkListBoxCompressionAlg, "Changes the order of the compression algorithmns. \n" +
            "Only effects 'Toogle Compression'. To disable a compression remove the file association, too");

        toolTip1.SetToolTip(chk_DecompressAll, "If set, all files will be decompressed regardless of the suffix.");


    }

    private void SetItemCheckState(int index, CheckState checkState) => chkListBoxCompressionAlg.SetItemCheckState(index, checkState);

    private void ComprSettingsClick(object sender, EventArgs e)
    {
        switch (chkListBoxCompressionAlg.SelectedItem)
        {
            case "gzip":
                GZipSettingsDialog gZipSettingsDialog = new()
                {
                    GZipSettings = Preferences.GZipSettings
                };
                if (gZipSettingsDialog.ShowDialog() == DialogResult.OK)
                    Preferences.GZipSettings = gZipSettingsDialog.GZipSettings;
                break;
            case "bzip2":
                BZip2SettingsDialog bZip2SettingsDialog = new()
                {
                    BZip2Settings = Preferences.BZip2Settings
                };
                if (bZip2SettingsDialog.ShowDialog() == DialogResult.OK)
                    Preferences.BZip2Settings = bZip2SettingsDialog.BZip2Settings;
                break;
            case "xz":
                XZSettingsDialog xzSettingsDialog = new()
                {
                    XZSettings = Preferences.XZSettings
                };
                if (xzSettingsDialog.ShowDialog() == DialogResult.OK)
                    Preferences.XZSettings = xzSettingsDialog.XZSettings;
                break;
            case "zstd":
                ZstdSettingsDialog zstdSettingsDialog = new()
                {
                    ZstdSettings = Preferences.ZstdSettings
                };
                if (zstdSettingsDialog.ShowDialog() == DialogResult.OK)
                    Preferences.ZstdSettings = zstdSettingsDialog.ZstdSettings;
                break;
        }
    }

    private void chkListBoxCompressionAlg_SelectedIndexChanged(object sender, EventArgs e)
    {
        btnSettings.Enabled = chkListBoxCompressionAlg.SelectedIndex >= 0;
        btnUp.Enabled = chkListBoxCompressionAlg.SelectedIndex > 0;
        btnDown.Enabled = chkListBoxCompressionAlg.SelectedIndex >= 0 &&
            chkListBoxCompressionAlg.SelectedIndex < (chkListBoxCompressionAlg.Items.Count - 1);
    }

    private void btnUp_Click(object sender, EventArgs e)
    {
        (chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex],
            chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex - 1]) =
            (chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex - 1],
            chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex]);
        chkListBoxCompressionAlg.SelectedIndex--;
    }

    private void btnDown_Click(object sender, EventArgs e)
    {
        (chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex],
              chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex + 1]) =
          (chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex + 1],
              chkListBoxCompressionAlg.Items[chkListBoxCompressionAlg.SelectedIndex]);
        chkListBoxCompressionAlg.SelectedIndex++;
    }

    private void btnDefault_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("This will reset ALL settings.", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            Preferences = Preferences.Default;
    }
}

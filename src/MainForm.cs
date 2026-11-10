namespace SLAL2OStim;

public class MainForm : Form
{
    readonly TextBox txtPack = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    readonly TextBox txtOut = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    readonly TextBox txtFnis = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    readonly TextBox txtPrefix = new() { Width = 160 };
    readonly CheckBox chkFnis = new() { Text = "Generate behavior hkx with FNIS for Modders:", AutoSize = true, Checked = true };
    readonly Button btnPack = new() { Text = "CONVERT PACK", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
    readonly Button btnTxt = new() { Text = "CONVERT TXT ONLY...", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
    readonly RichTextBox log = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 9f),
        BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Gainsboro, WordWrap = false,
    };

    public MainForm(string startPath)
    {
        Text = "SLAL to OStim - OCreatures";
        ClientSize = new Size(860, 680);
        MinimumSize = new Size(600, 480);
        Font = new Font("Segoe UI", 9f);
        AllowDrop = true;

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void AddRow(Control label, TextBox box, Action browse)
        {
            grid.Controls.Add(label, 0, grid.RowCount++);
            grid.SetColumnSpan(label, 2);
            grid.Controls.Add(box, 0, grid.RowCount);
            var b = new Button { Text = "Browse...", AutoSize = true };
            b.Click += (_, _) => browse();
            grid.Controls.Add(b, 1, grid.RowCount++);
        }

        AddRow(new Label { Text = "SLAL pack folder (contains meshes / SLAnims / Textures) - or drag a folder here:", AutoSize = true },
            txtPack, () => PickFolder(txtPack, true));
        AddRow(new Label { Text = "Output folder:", AutoSize = true }, txtOut, () => PickFolder(txtOut, false));
        AddRow(chkFnis, txtFnis, PickExe);

        var prefixRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        prefixRow.Controls.Add(new Label { Text = "Scene prefix (empty = from SLAL json name, e.g. Billyy):", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        prefixRow.Controls.Add(txtPrefix);
        grid.Controls.Add(prefixRow, 0, grid.RowCount++);
        grid.SetColumnSpan(prefixRow, 2);

        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, 8, 0, 4) };
        btnPack.BackColor = Color.FromArgb(76, 175, 80);
        btnPack.ForeColor = Color.White;
        btnTxt.BackColor = Color.FromArgb(74, 107, 175);
        btnTxt.ForeColor = Color.White;
        foreach (var b in new[] { btnPack, btnTxt })
        {
            b.FlatStyle = FlatStyle.Flat;
            b.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            buttons.Controls.Add(b);
        }
        grid.Controls.Add(buttons, 0, grid.RowCount++);
        grid.SetColumnSpan(buttons, 2);

        var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 8) };
        logPanel.Controls.Add(log);
        Controls.Add(logPanel);
        Controls.Add(grid);

        txtFnis.Text = Converter.DefaultFnisExe;
        if (!File.Exists(txtFnis.Text))
            Log("NOTE: FNIS for Modders not found next to the app - keep the FNIS folder beside SLAL2OStim.exe or browse to GenerateFNISforModders.exe.");

        btnPack.Click += (_, _) => StartPack();
        btnTxt.Click += (_, _) => StartTxt();
        DragEnter += (_, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) SetInput(paths[0]);
        };
        if (!string.IsNullOrEmpty(startPath)) SetInput(startPath);
    }

    void SetInput(string path)
    {
        if (Directory.Exists(path))
        {
            txtPack.Text = path;
            txtOut.Text = path.TrimEnd('\\', '/') + "_OStim";
        }
        else if (File.Exists(path) && path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            Run(c => c.ConvertTxt(path));
    }

    void PickFolder(TextBox box, bool isPack)
    {
        using var dlg = new FolderBrowserDialog { UseDescriptionForTitle = true, Description = isPack ? "Select SLAL pack" : "Select output folder" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (isPack) SetInput(dlg.SelectedPath);
        else box.Text = dlg.SelectedPath;
    }

    void PickExe()
    {
        using var dlg = new OpenFileDialog { Title = "GenerateFNISforModders.exe", Filter = "exe|*.exe" };
        if (dlg.ShowDialog(this) == DialogResult.OK) txtFnis.Text = dlg.FileName;
    }

    void Log(string msg)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(msg)); return; }
        log.AppendText(msg.Replace("\n", Environment.NewLine) + Environment.NewLine);
    }

    void StartPack()
    {
        if (string.IsNullOrWhiteSpace(txtPack.Text) || string.IsNullOrWhiteSpace(txtOut.Text))
        {
            Log("Select both folders first.");
            return;
        }
        string pack = txtPack.Text.Trim(), outDir = txtOut.Text.Trim();
        Run(c => c.Convert(pack, outDir));
    }

    void StartTxt()
    {
        using var dlg = new OpenFileDialog { Title = "Select FNIS list txt", Filter = "FNIS list|*.txt|All files|*.*", Multiselect = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var files = dlg.FileNames;
        Run(c => files.Select(c.ConvertTxt).ToList().All(ok => ok));
    }

    void Run(Func<Converter, bool> job)
    {
        log.Clear();
        var conv = new Converter
        {
            Log = Log,
            FnisExe = txtFnis.Text.Trim(),
            GenBehaviors = chkFnis.Checked,
            Prefix = txtPrefix.Text.Trim(),
        };
        btnPack.Enabled = btnTxt.Enabled = false;
        UseWaitCursor = true;
        Task.Run(() =>
        {
            try { job(conv); }
            catch (Exception e) { Log("ERROR: " + e.Message); }
        }).ContinueWith(_ => BeginInvoke(() =>
        {
            btnPack.Enabled = btnTxt.Enabled = true;
            UseWaitCursor = false;
        }));
    }
}

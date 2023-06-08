using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace File_Transfer_LoRa
{
    public partial class Filte : Form
    {
        string Upload_File;

        int chuck = 250;
        Byte[,] File_Buffer = new Byte[18837, 250];

        string package = "", last_pack = "";
        
        bool loop_enable = true, sending = false, downloading = false;
        
        int File_Size = 0;

        int count_size = 0,                                                         // Conta quantos bytes foram lidos
            count_slices = 0;                                                       // Conta quantas fatias foram lidas
                                                                                    // Conta 228 bytes, uso local 


        string last_download = "";

        bool download_completed = false;
        string File_Name_Downloaded;
        bool download_initialized = false;
        int counter = 0;
        int interval = 30000;
        bool file_buff = false;

        string final_package = "";
        //--------------------

        int pack_up, pack_down;
        int pb_up, pb_down;

        int pb_max_up, pb_max_down;
        int real_size_file_download = 0;

        public Filte()
        {
            InitializeComponent();
            for(int i = 1; i < 7; ++i)
                comboBox2.Items.Add(Convert.ToString(i * 5));
            comboBox1.Items.Add("Tx");
            comboBox1.Items.Add("Rx");
            comboBox2.Text = Properties.Settings.Default.interval;
            progressBar1.Minimum = 0;
        }

        private string path()
        {
            try 
            {
                OpenFileDialog openFileDialog1 = new OpenFileDialog
                {
                    InitialDirectory = @"D:\",
                    Title = "Browse Text Files",

                    CheckFileExists = true,
                    CheckPathExists = true,

                    //DefaultExt = "txt",
                    //Filter = "txt files (*.txt)|*.txt",
                    FilterIndex = 2,
                    RestoreDirectory = true,

                    ReadOnlyChecked = true,
                    ShowReadOnly = true
                };

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                    return openFileDialog1.FileName;
                else
                    return "";
            }
            catch { return ""; }
        }
        
        int counteri = 0;

        private void upload() 
        {
            Byte[] bytes = File.ReadAllBytes(@".\temp\file.zip");              // pega os bytes do arquivo
            String buff_file64 = Convert.ToBase64String(bytes);         // transforma os bytes do arquivo em um texto completo em base4
            char[] cfile = buff_file64.ToCharArray();                   // converte o texto completo em char array
            Byte[] file = new Byte[cfile.Length];                       // converte o char array para byte array para que possa ser tratado no fatiador

            for (int i = 0; i < cfile.Length; ++i)
                file[i] = Convert.ToByte(cfile[i]);

            File_Size = file.Length;

            int slice = File_Size / chuck;                                          // 6.430.860  / 232 = 27.719
            int rest_of_the_slices = File_Size - (slice * chuck);
            int File_Buffer_Size = 0;

            pb_up = slice + rest_of_the_slices;
            sending = true;
            
            for (int i = 0; i < 18837; ++i)
            {
                for (int k = 0; k < chuck; ++k)
                {
                    if (File_Buffer_Size < File_Size)
                    {
                        File_Buffer[i, k] = file[k];
                        ++File_Buffer_Size;
                    }
                    else
                        break;
                }
            }

            Thread.Sleep(interval);

            while (loop_enable)
            {
                for (int i = 0; i < slice; ++i)
                {

                    int mult = i * chuck;
                    if (i == 0)
                        mult = 0;

                    counteri = i;
                    serialPort1.Write(file, mult, chuck);
                    pack_up = i;

                    if (!loop_enable)
                        break;

                    Thread.Sleep(interval);

                }
                serialPort1.Write(file, slice * chuck, rest_of_the_slices);
                Thread.Sleep(interval);
                loop_enable = false;
            }

            pack_up = pb_up;
            serialPort1.Close();
            sending = false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (pb_max_up != pb_up) 
            {
                pb_max_up = pb_up;
                progressBar1.Maximum = pb_max_up;
            }

            if (pack_up <= pb_up)
                progressBar1.Value = pack_up;

            if (download_completed) 
            {
                save_download();
            }

            if (download_initialized) 
            {
                if (counter < interval / 1000)
                    ++counter;
                else 
                {
                    if (package == last_pack) 
                    {
                        download_initialized = false;
                        last_pack = "";
                        final_package = package;
                        
                        decompress();
                        package = "";
                    }
                    last_pack = package;
                    counter = 0;
                }
            }
        }

        private void createDirectory(string val) 
        {
            DirectoryInfo di = Directory.CreateDirectory(@".\");
            di.CreateSubdirectory(val);
        }

        private string getFilename(string val)
        {
            return val.Remove(0, val.LastIndexOf("\\") + 1);
        }

        private string getFilepath(string val)
        {
            return val.Remove(val.LastIndexOf("\\"));
        }

        private void select_file_Click(object sender, EventArgs e)
        {
            Upload_File = path();
            string file_name = getFilename(Upload_File);
            name_path1.Text = "File: " + file_name;

            createDirectory("temp");

            if (File.Exists(@".\temp\file.zip"))
                File.Delete(@".\temp\file.zip");
            else if (File.Exists(@".\file.zip"))
                File.Delete(@".\file.zip");

            ZipFile.CreateFromDirectory(@".\temp", @".\file.zip");
            using (ZipArchive zip = ZipFile.Open("file.zip", ZipArchiveMode.Update))
                zip.CreateEntryFromFile(Upload_File, file_name);

            File.Move(@".\file.zip", @".\temp\file.zip");

            try
            {
                if (Upload_File != "" || Upload_File != null)
                    select_file.IconFont = FontAwesome.Sharp.IconFont.Solid;
                else
                    select_file.IconFont = FontAwesome.Sharp.IconFont.Regular;
            }
            catch 
            { 
            }

            if (File.Exists(@".\file.zip"))
                File.Delete(@".\file.zip");
        }

        private void decompress(string val = @".\downloaded\file.zip") 
        {
            try
            {
                createDirectory("downloaded");
                string buff = "";

                if (package != "")
                    buff = package;
                else
                    buff = last_pack;

                Byte[] file = Convert.FromBase64String(buff);
                File.WriteAllBytes(val, file);

                string path = getFilepath(val); // Remove o nome do arquivo e fica apenas com o diretório
                string filename = "";
                using (ZipArchive archive = ZipFile.OpenRead(val))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        filename = entry.FullName;
                        entry.ExtractToFile(Path.Combine(path, entry.FullName), true); //tive que por esse remove por conta que ficou com uma \ o que dava o erro
                    }
                }
                File.Delete(val);

                if (last_download != name_path2.Text) 
                {
                    if (filename == "")
                        filename = getFilename(val);
                   name_path2.Text = "File: " + filename;
                   last_download = name_path2.Text;
                   save_file.IconFont = FontAwesome.Sharp.IconFont.Solid;
                }
            }
            catch 
            {
            }
        }

        private void save_file_Click(object sender, EventArgs e)
        {
            try
            {
                saveFileDialog1.Title = "Save File";
                saveFileDialog1.Filter = "ZIP Files (*.zip)|*.zip";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK) 
                {
                    decompress(saveFileDialog1.FileName);
                    decompress();
                }
            }
            catch(Exception err)
            {
                //MessageBox.Show(err.ToString());
            }
        }

        private void select_port_MouseClick(object sender, MouseEventArgs e)
        {
            select_port.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            select_port.Items.AddRange(ports);
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.interval = comboBox2.Text;
            Properties.Settings.Default.Save();
        }

        private void Upload_Click(object sender, EventArgs e)
        {
            try
            {
                if (select_port.Text != "")
                {
                    serialPort1.BaudRate = 9600;

                    serialPort1.PortName = select_port.Text;
                    serialPort1.Open();
                    Start.Text = "Start";
                    if (comboBox1.Text == "Tx")
                    {
                        Start.Text = "Uploading";
                        serialPort1.DiscardOutBuffer();
                        ThreadStart RS = new ThreadStart(upload);
                        Thread TRS = new Thread(RS);
                        TRS.Start();
                    }
                    else if(comboBox1.Text == "Rx")
                    {
                        Start.Text = "Downloading";
                        ThreadStart RS = new ThreadStart(download);
                        Thread TRS = new Thread(RS);
                        TRS.Start();
                    }
                   
                }
                else if (select_port.Text == "")
                    MessageBox.Show("COM invalida");
            }
            catch (Exception err)
            {
                MessageBox.Show(err.ToString());
            }

            interval = Convert.ToInt32(comboBox2.Text) * 1000;
        }

        private void download() 
        {
            while (loop_enable) 
            {
                if (!download_completed) 
                {
                    int buff = serialPort1.ReadChar();
                    package += Convert.ToString(Convert.ToChar(buff));
                    if (package != "")
                        download_initialized = true;
                }
            }
        }

        private void save_download() 
        {
            int count_local_size = 0;
            Byte[] file_download_buffer = new Byte[count_size];
            for (int i = 0; i < count_slices; ++i)
            {
                for (int k = 0; k < chuck; ++k)
                {
                    if (count_local_size < count_size)
                    {
                        file_download_buffer[count_local_size] = File_Buffer[i, k];
                        ++count_local_size;
                    }
                    else
                        break;
                }
            }

            File_Name_Downloaded = $"file_downloaded {DateTime.Now.ToString("dd_MM_yyyy HH_mm_ss")}.zip";

            File.WriteAllBytes($@".\{File_Name_Downloaded}", file_download_buffer);

            count_size = 0;
            count_slices = 0;
            download_completed = false;
        }

        private void select_file_MouseHover(object sender, EventArgs e)
        {
            select_file.IconChar = FontAwesome.Sharp.IconChar.FolderOpen;
        }

        private void select_file_MouseLeave(object sender, EventArgs e)
        {
            select_file.IconChar = FontAwesome.Sharp.IconChar.FolderBlank;
        }

        private void save_file_MouseHover(object sender, EventArgs e)
        {
            save_file.IconChar = FontAwesome.Sharp.IconChar.FolderOpen;
        }

        private void save_file_MouseLeave(object sender, EventArgs e)
        {
            save_file.IconChar = FontAwesome.Sharp.IconChar.FolderBlank;
        }

        private void maskedTextBox2_MouseClick(object sender, MouseEventArgs e)
        {
            maskedTextBox2.Text = maskedTextBox2.Text.Replace(" ", "");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            loop_enable = false;
            serialPort1.Close();
        }
    }
}

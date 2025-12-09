using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management; 
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace DoTe101
{
    public partial class Form1 : Form
    {
        private ulong totalRamKB;

        CancellationTokenSource iptalKaynagi;

        [DllImport("psapi.dll", EntryPoint = "EmptyWorkingSet", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        public Form1()
        {
            InitializeComponent();
        }

        private void ButonlariAyarla(bool aktif)
        {
            btnCpuBenchmark.Enabled = aktif;
            btnDiskTest.Enabled = aktif;
        }
        private void MedyaTara(string baslangicYolu)
        {
            string[] videoUzantilari = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" };
            string[] resimUzantilari = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".raw", ".tiff", ".heic" };

            long videoSinir = 1000 * 1024 * 1024; 
            long resimSinir = 45 * 1024 * 1024;  

            try
            {
                if (baslangicYolu.Contains("AppData") || baslangicYolu.Contains("Windows") || baslangicYolu.Contains("Program Files")) return;

                DirectoryInfo di = new DirectoryInfo(baslangicYolu);

                foreach (FileInfo fi in di.GetFiles())
                {
                    try
                    {
                        string uzanti = fi.Extension.ToLower();
                        string tur = "";
                        bool eklenecek = false;

                        if (videoUzantilari.Contains(uzanti) && fi.Length >= videoSinir)
                        {
                            tur = "Video 🎥";
                            eklenecek = true;
                        }
                        else if (resimUzantilari.Contains(uzanti) && fi.Length >= resimSinir)
                        {
                            tur = "Görsel 📷";
                            eklenecek = true;
                        }

                        if (eklenecek)
                        {
                            this.Invoke(new Action(() =>
                            {
                                double boyutMB = Math.Round(fi.Length / (1024.0 * 1024.0), 2);

                                ListViewItem item = new ListViewItem(fi.Name); 
                                item.SubItems.Add(tur);                         
                                item.SubItems.Add(boyutMB.ToString() + " MB");  
                                item.SubItems.Add(fi.DirectoryName);            
                                item.Tag = fi.FullName; 

                                lvMedya.Items.Add(item);
                            }));
                        }
                    }
                    catch { }
                }

                foreach (DirectoryInfo altKlasor in di.GetDirectories())
                {
                    try
                    {
                        if ((altKlasor.Attributes & FileAttributes.Hidden) == 0)
                        {
                            MedyaTara(altKlasor.FullName);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void ProgramlariListele()
        {
            lvProgramlar.Items.Clear();

            string[] registryYollari = {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

            DateTime altLimit = DateTime.Now.AddMonths(-6);

            foreach (string yol in registryYollari)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(yol))
                {
                    if (key == null) continue;

                    foreach (string altAnahtarAdi in key.GetSubKeyNames())
                    {
                        using (RegistryKey altAnahtar = key.OpenSubKey(altAnahtarAdi))
                        {
                            if (altAnahtar == null) continue;

                            try
                            {
                                object objName = altAnahtar.GetValue("DisplayName");
                                if (objName == null) continue;

                                string ad = objName.ToString();
                                string yayimci = altAnahtar.GetValue("Publisher")?.ToString() ?? "---";

                                string tarihHam = altAnahtar.GetValue("InstallDate")?.ToString();
                                string tarihFormatli = "---";
                                DateTime? kurulumTarihi = null;

                                if (!string.IsNullOrEmpty(tarihHam))
                                {
                                    if (tarihHam.Length == 8 &&
                                        int.TryParse(tarihHam.Substring(0, 4), out int yil) &&
                                        int.TryParse(tarihHam.Substring(4, 2), out int ay) &&
                                        int.TryParse(tarihHam.Substring(6, 2), out int gun))
                                    {
                                        kurulumTarihi = new DateTime(yil, ay, gun);
                                        tarihFormatli = kurulumTarihi.Value.ToString("dd.MM.yyyy");
                                    }
                                    else if (DateTime.TryParse(tarihHam, out DateTime dt))
                                    {
                                        kurulumTarihi = dt;
                                        tarihFormatli = dt.ToString("dd.MM.yyyy");
                                    }
                                    else
                                    {
                                        tarihFormatli = tarihHam;
                                    }
                                }

                                string kaldirmaKodu = altAnahtar.GetValue("UninstallString")?.ToString();

                                ListViewItem item = new ListViewItem(ad);
                                item.SubItems.Add(yayimci);
                                item.SubItems.Add(tarihFormatli);
                                item.Tag = kaldirmaKodu;

                                if (kurulumTarihi.HasValue && kurulumTarihi.Value < altLimit)
                                {
                                    item.BackColor = Color.LightYellow;
                                    item.ForeColor = Color.DarkRed;
                                }

                                lvProgramlar.Items.Add(item);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }

        private void DosyaTara(string baslangicYolu, long minBoyutByte)
        {
            try
            {
                if (baslangicYolu.Contains("AppData") ||
                    baslangicYolu.Contains("Windows") ||
                    baslangicYolu.Contains("Program Files"))
                {
                    return;
                }

                DirectoryInfo di = new DirectoryInfo(baslangicYolu);

                foreach (FileInfo fi in di.GetFiles())
                {
                    try
                    {
                        // 2. GÜVENLİK KONTROLÜ: Kritik uzantıları atla
                        string uzanti = fi.Extension.ToLower();
                        if (uzanti == ".sys" || uzanti == ".dll" || uzanti == ".exe" || uzanti == ".msi" || uzanti == ".db")
                        {
                            continue;
                        }

                        if (fi.Length >= minBoyutByte)
                        {
                            this.Invoke(new Action(() =>
                            {
                                double boyutMB = Math.Round(fi.Length / (1024.0 * 1024.0), 2);

                                ListViewItem item = new ListViewItem(fi.Name);
                                item.SubItems.Add(boyutMB.ToString() + " MB");
                                item.SubItems.Add(fi.DirectoryName);
                                item.Tag = fi.FullName;

                                lvBuyukDosyalar.Items.Add(item);
                            }));
                        }
                    }
                    catch { }
                }

                foreach (DirectoryInfo altKlasor in di.GetDirectories())
                {
                    try
                    {
                        if ((altKlasor.Attributes & FileAttributes.Hidden) == 0)
                        {
                            DosyaTara(altKlasor.FullName, minBoyutByte);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        private void GecmisGuncelle(string tur, string yeniSkor)
        {
            string tarihliSkor = $"{yeniSkor} ({DateTime.Now.ToString("HH:mm")})";

            if (tur == "CPU")
            {
                Properties.Settings.Default.CpuSkor2 = Properties.Settings.Default.CpuSkor1;
                Properties.Settings.Default.CpuSkor1 = tarihliSkor;
                Properties.Settings.Default.Save();

                lblCpuGecmis.Text = $"Son:\n1. {Properties.Settings.Default.CpuSkor1}\n2. {Properties.Settings.Default.CpuSkor2}";
            }
            else if (tur == "SSD")
            {
                Properties.Settings.Default.SsdSkor2 = Properties.Settings.Default.SsdSkor1;
                Properties.Settings.Default.SsdSkor1 = tarihliSkor;
                Properties.Settings.Default.Save();

                lblSsdGecmis.Text = $"Son:\n1. {Properties.Settings.Default.SsdSkor1}\n2. {Properties.Settings.Default.SsdSkor2}";
            }
        }

        private void GecmisleriYukle()
        {
            string c1 = Properties.Settings.Default.CpuSkor1;
            string c2 = Properties.Settings.Default.CpuSkor2;
            if (c1 != "-" || c2 != "-")
                lblCpuGecmis.Text = $"Son:\n1. {c1}\n2. {c2}";

            string s1 = Properties.Settings.Default.SsdSkor1;
            string s2 = Properties.Settings.Default.SsdSkor2;
            if (s1 != "-" || s2 != "-")
                lblSsdGecmis.Text = $"Son:\n1. {s1}\n2. {s2}";
        }

        private void KlasoruGuvenleTemizle(string klasorYolu, bool altKlasorleriSil)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(klasorYolu);
                foreach (FileInfo file in di.GetFiles())
                {
                    try { file.Delete(); } catch { }
                }

                if (altKlasorleriSil)
                {
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        try { dir.Delete(true); } catch { }
                    }
                }
            }
            catch { }
        }

        private void PuanlariGetir()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_WinSAT");
                foreach (ManagementObject obj in searcher.Get())
                {
                    lblScoreCPU.Text = obj["CPUScore"].ToString();
                    lblScoreRAM.Text = obj["MemoryScore"].ToString();
                    lblScoreGPU.Text = obj["GraphicsScore"].ToString();
                    lblScoreDisk.Text = obj["DiskScore"].ToString();
                    lblBaseScore.Text = obj["WinSPRLevel"].ToString();
                }
            }
            catch { lblBaseScore.Text = "?"; }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                lblIsletimSistemi.Text = Environment.OSVersion.VersionString;
                lblMakineAdi.Text = Environment.MachineName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Temel sistem bilgileri alınırken hata oluştu: " + ex.Message);
            }

            try
            {
                ManagementObjectSearcher anakartSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (ManagementObject obj in anakartSearcher.Get())
                {
                    lblAnakart.Text = obj["Manufacturer"].ToString() + " " + obj["Product"].ToString();
                    break;
                }
            }
            catch (Exception ex)
            {
                lblAnakart.Text = "Anakart bilgisi alınamadı: " + ex.Message;
            }

            try
            {
                ManagementObjectSearcher cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
                lblIslemci.Text = "";
                foreach (ManagementObject obj in cpuSearcher.Get())
                {
                    string name = obj["Name"].ToString().Trim();
                    string cores = obj["NumberOfCores"].ToString();
                    double speedGHz = Math.Round(Convert.ToUInt32(obj["MaxClockSpeed"]) / 1000.0, 2);
                    lblIslemci.Text += $"{name} ({cores} Çekirdek, {speedGHz} GHz)\r\n";
                }
                lblIslemci.Text = lblIslemci.Text.TrimEnd();
            }
            catch (Exception ex)
            {
                lblIslemci.Text = "İşlemci bilgisi alınamadı: " + ex.Message;
            }

            try
            {
                ManagementObjectSearcher ramSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in ramSearcher.Get())
                {
                    if (obj["TotalPhysicalMemory"] != null)
                    {
                        ulong totalMemoryBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                        double totalMemoryGB = Math.Round(totalMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2);

                        lblRam.Text = totalMemoryGB.ToString("N2") + " GB";
                        this.totalRamKB = totalMemoryBytes / 1024;
                        lblToplamRAM.Text = totalMemoryGB.ToString("N2") + " GB";
                        pbRamKullanim.Maximum = (int)(this.totalRamKB / 1024);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                lblRam.Text = "RAM bilgisi alınamadı: " + ex.Message;
                lblToplamRAM.Text = "Hata.";
            }

            try
            {
                ManagementObjectSearcher ekranKartiSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                lblEkranKarti.Text = "";
                foreach (ManagementObject obj in ekranKartiSearcher.Get())
                {
                    lblEkranKarti.Text += obj["Name"].ToString().Trim() + "\r\n";
                }
                lblEkranKarti.Text = lblEkranKarti.Text.TrimEnd();
            }
            catch (Exception ex)
            {
                lblEkranKarti.Text = "Ekran kartı bilgisi alınamadı: " + ex.Message;
            }

            try
            {
                string agKarti = null;
                string bluetoothKarti = null;
                ManagementObjectSearcher agSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter = True");
                foreach (ManagementObject obj in agSearcher.Get())
                {
                    string adapterName = obj["Name"].ToString();
                    if (bluetoothKarti == null && adapterName.ToLower().Contains("bluetooth"))
                    {
                        bluetoothKarti = adapterName;
                    }
                    else if (agKarti == null && !adapterName.ToLower().Contains("bluetooth"))
                    {
                        agKarti = adapterName;
                    }
                    if (agKarti != null && bluetoothKarti != null)
                    {
                        break;
                    }
                }
                string sonuc = "";
                if (agKarti != null) sonuc += agKarti + "\r\n";
                if (bluetoothKarti != null) sonuc += bluetoothKarti;
                else sonuc += "Bluetooth bulunamadı.";

                if (string.IsNullOrEmpty(sonuc)) sonuc = "Fiziksel ağ kartı bulunamadı.";
                lblAgKarti.Text = sonuc.Trim();
            }
            catch (Exception ex)
            {
                lblAgKarti.Text = "Ağ bilgisi alınamadı: " + ex.Message;
            }

            try
            {
                ManagementObjectSearcher diskSearcher = new ManagementObjectSearcher("SELECT Model, Size, InterfaceType FROM Win32_DiskDrive");
                lblDisk.Text = "";
                foreach (ManagementObject drive in diskSearcher.Get())
                {
                    string model = "Bilinmeyen Model";
                    if (drive["Model"] != null) model = drive["Model"].ToString().Trim();

                    string interfaceType = "Bilinmeyen Arayüz";
                    if (drive["InterfaceType"] != null) interfaceType = drive["InterfaceType"].ToString().Trim();

                    ulong totalSizeBytes = Convert.ToUInt64(drive["Size"]);
                    double totalSizeGB = Math.Round(totalSizeBytes / (1000.0 * 1000.0 * 1000.0), 0);

                    lblDisk.Text += $"{totalSizeGB} GB {model} ({interfaceType})\r\n";
                }
                lblDisk.Text = lblDisk.Text.TrimEnd();
            }
            catch (Exception ex)
            {
                lblDisk.Text = "Sistem Diski modeli alınamadı: " + ex.Message;
            }

            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    if (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable)
                    {
                        Label lblDriveTitle = new Label();
                        lblDriveTitle.Text = $"Sürücü {drive.Name} ({drive.VolumeLabel})";
                        lblDriveTitle.Font = new Font(this.Font, FontStyle.Bold);
                        lblDriveTitle.AutoSize = true;
                        flpDiskler.Controls.Add(lblDriveTitle);

                        ProgressBar pbDrive = new ProgressBar();
                        pbDrive.Width = flpDiskler.Width - 30;
                        pbDrive.Maximum = 100;
                        long totalSize = drive.TotalSize;
                        long freeSpace = drive.TotalFreeSpace;
                        long usedSpace = totalSize - freeSpace;
                        double yuzde = ((double)usedSpace / totalSize) * 100.0;
                        pbDrive.Value = (int)yuzde;
                        flpDiskler.Controls.Add(pbDrive);

                        double totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        double usedGB = usedSpace / (1024.0 * 1024.0 * 1024.0);
                        Label lblDriveDetails = new Label();
                        lblDriveDetails.Text = $"Kullanılan: {usedGB:N2} GB / {totalGB:N2} GB";
                        lblDriveDetails.AutoSize = true;
                        lblDriveDetails.Margin = new Padding(0, 0, 0, 15);
                        flpDiskler.Controls.Add(lblDriveDetails);
                    }
                }
            }
            catch (Exception ex)
            {
                Label lblError = new Label();
                lblError.Text = "Sürücüler listelenirken hata oluştu: " + ex.Message;
                lblError.AutoSize = true;
                flpDiskler.Controls.Add(lblError);
            }

            try
            {
                notifyIcon_Main.Icon = this.Icon;
                DateTime kayitliTarih = Properties.Settings.Default.TozTemizlikTarihi;
                int kayitliAy = Properties.Settings.Default.TozHatirlatmaAyi;

                if (kayitliTarih.Year > 1950) dtpToz.Value = kayitliTarih;
                numTozAy.Value = kayitliAy;

                if (kayitliAy > 0 && kayitliTarih.Year > 1950)
                {
                    DateTime sonrakiBakimTarihi = kayitliTarih.AddMonths(kayitliAy);
                    if (sonrakiBakimTarihi <= DateTime.Now)
                    {
                        int gecenAy = ((DateTime.Now.Year - kayitliTarih.Year) * 12) + DateTime.Now.Month - kayitliTarih.Month;
                        if (DateTime.Now.Day < kayitliTarih.Day) gecenAy--;

                        notifyIcon_Main.ShowBalloonTip(20000, "Toz Temizliği Hatırlatması",
                            $"Son temizlikten bu yana {gecenAy} ay geçmiş!\nBakım zamanı.", ToolTipIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                if (notifyIcon_Main.Icon == null) notifyIcon_Main.Icon = this.Icon;
                notifyIcon_Main.ShowBalloonTip(5000, "Ayar Yükleme Hatası", "Ayarlar yüklenemedi: " + ex.Message, ToolTipIcon.Error);
            }

            try
            {
                DateTime kayitliMacunTarihi = Properties.Settings.Default.MacunTarihi;
                int kayitliMacunAyi = Properties.Settings.Default.MacunHatirlatmaAyi;
                string kayitliMarka = Properties.Settings.Default.MacunMarkasi;
                string kayitliYontem = Properties.Settings.Default.MacunSurusYontemi;

                if (kayitliMacunTarihi.Year > 1950) dtpMacun.Value = kayitliMacunTarihi;
                numMacunAy.Value = kayitliMacunAyi;
                if (kayitliMarka != null) cmbMacunMarkasi.Text = kayitliMarka;
                if (kayitliYontem != null) cmbSurusYontemi.Text = kayitliYontem;

                if (kayitliMacunAyi > 0 && kayitliMacunTarihi.Year > 1950)
                {
                    DateTime sonrakiMacunTarihi = kayitliMacunTarihi.AddMonths(kayitliMacunAyi);
                    if (sonrakiMacunTarihi <= DateTime.Now)
                    {
                        int gecenAy = ((DateTime.Now.Year - kayitliMacunTarihi.Year) * 12) + DateTime.Now.Month - kayitliMacunTarihi.Month;
                        if (DateTime.Now.Day < kayitliMacunTarihi.Day) gecenAy--;

                        notifyIcon_Main.ShowBalloonTip(20000, "Termal Macun Hatırlatması",
                            $"Son değişimden bu yana {gecenAy} ay geçmiş!\nİşlemci sağlığı için değişim zamanı gelmiş olabilir.", ToolTipIcon.None);
                    }
                }
            }
            catch (Exception ex)
            {
                notifyIcon_Main.ShowBalloonTip(5000, "Macun Ayar Hatası", "Kaydedilmiş macun ayarları yüklenemedi: " + ex.Message, ToolTipIcon.Error);
            }

            PuanlariGetir();
            GecmisleriYukle();
            ProgramlariListele();
        }

        private void timerRAM_Tick(object sender, EventArgs e)
        {
            try
            {
                ManagementObjectSearcher ramSearcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in ramSearcher.Get())
                {
                    ulong freeRamKB = Convert.ToUInt64(obj["FreePhysicalMemory"]);
                    ulong usedRamKB = this.totalRamKB - freeRamKB;
                    int usedRamMB = (int)(usedRamKB / 1024);

                    if (usedRamMB <= pbRamKullanim.Maximum) pbRamKullanim.Value = usedRamMB;

                    double usedRamGB = usedRamKB / (1024.0 * 1024.0);
                    double yuzde = ((double)usedRamMB / pbRamKullanim.Maximum) * 100.0;
                    lblKullanilanRAM.Text = $"{usedRamGB.ToString("N2")} GB ({yuzde.ToString("N0")} %)";
                    break;
                }
            }
            catch { pbRamKullanim.Value = 0; }
        }

        private void btnDosyaTemizle_Click(object sender, EventArgs e)
        {
            btnDosyaTemizle.Enabled = false;
            btnDosyaTemizle.Text = "Temizleniyor, lütfen bekleyin...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                KlasoruGuvenleTemizle(Path.GetTempPath(), true);
                KlasoruGuvenleTemizle(@"C:\Windows\Temp", true);
                KlasoruGuvenleTemizle(Environment.GetFolderPath(Environment.SpecialFolder.Recent), false);
                KlasoruGuvenleTemizle(@"C:\Windows\Prefetch", false);
                MessageBox.Show("Temizlik Tamamlandı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Temizlik sırasında bir hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDosyaTemizle.Enabled = true;
                btnDosyaTemizle.Text = "Geçici Dosyaları Temizle";
                this.Cursor = Cursors.Default;
            }
        }

        private void btnRamTemizle_Click(object sender, EventArgs e)
        {
            btnRamTemizle.Enabled = false;
            btnRamTemizle.Text = "RAM Temizleniyor...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Process[] tumProcessler = Process.GetProcesses();
                int aktifOturum = Process.GetCurrentProcess().SessionId;

                foreach (Process p in tumProcessler)
                {
                    try
                    {
                        if (p.SessionId == aktifOturum)
                        {
                            EmptyWorkingSet(p.Handle);
                        }
                    }
                    catch
                    {
                        
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("RAM temizleme sırasında bir hata oluştu: " + ex.Message,
                                "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRamTemizle.Enabled = true;
                btnRamTemizle.Text = "RAM Temizle";
                this.Cursor = Cursors.Default;

                MessageBox.Show("RAM temizleme tamamlandı!",
                                "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }



        private void btnVideoRehberiAc_Click(object sender, EventArgs e)
        {
            string secim = cmbKasaTipi.SelectedItem as string;
            string url = "https://www.youtube.com/results?search_query=pc+cleaning+guide";
            if (secim != null && secim.Contains("Laptop")) url = "https://www.youtube.com/watch?v=jQG_R-kE1Zc";
            else if (secim != null) url = "https://www.youtube.com/watch?v=g9VRl-yF-G0";

            try { Process.Start(url); } catch { }
        }

        private void btnTozTarihiKaydet_Click(object sender, EventArgs e)
        {
            try
            {
                Properties.Settings.Default.TozTemizlikTarihi = dtpToz.Value;
                Properties.Settings.Default.TozHatirlatmaAyi = (int)numTozAy.Value;
                Properties.Settings.Default.Save();
                MessageBox.Show("Kaydedildi!", "Başarılı");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private void btnMacunTarihiKaydet_Click(object sender, EventArgs e)
        {
            try
            {
                Properties.Settings.Default.MacunTarihi = dtpMacun.Value;
                Properties.Settings.Default.MacunHatirlatmaAyi = (int)numMacunAy.Value;
                Properties.Settings.Default.MacunMarkasi = cmbMacunMarkasi.Text;
                Properties.Settings.Default.MacunSurusYontemi = cmbSurusYontemi.Text;
                Properties.Settings.Default.Save();
                MessageBox.Show("Kaydedildi!", "Başarılı");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private void btnMacunTavsiyesi_Click(object sender, EventArgs e)
        {
            string marka = cmbMacunMarkasi.Text.ToLower();
            string yontem = (cmbSurusYontemi.SelectedItem as string ?? "").ToLower();
            string mYorum = "Bilinmiyor.", yYorum = "Seçilmedi.";

            if (marka.Contains("bakkal") || marka.Contains("50 kuruş")) mYorum = "ÇÖP! ACİLEN DEĞİŞTİR!";
            else if (marka.Contains("arctic") || marka.Contains("noctua")) mYorum = "Mükemmel tercih!";

            if (yontem.Contains("x") || yontem.Contains("nokta")) yYorum = "En iyi yöntem.";
            else if (yontem.Contains("yayarak")) yYorum = "Riskli, dikkat et.";

            MessageBox.Show($"Marka: {mYorum}\nYöntem: {yYorum}", "Tavsiye");
        }

        private void btnBakimSifirla_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Emin misin?", "Sıfırla", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Properties.Settings.Default.Reset();
                MessageBox.Show("Sıfırlandı.");
            }
        }

        private async void btnGeriYuklemeYedegi_Click(object sender, EventArgs e)
        {
            btnGeriYuklemeYedegi.Enabled = false;
            btnGeriYuklemeYedegi.Text = "Oluşturuluyor...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                string msg = await Task.Run(() =>
                {
                    try
                    {
                        ManagementScope scope = new ManagementScope(@"root\default");
                        ManagementClass sysRestore = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
                        ManagementBaseObject inParams = sysRestore.GetMethodParameters("CreateRestorePoint");
                        inParams["Description"] = "DoTe101 Yedek - " + DateTime.Now.ToString();
                        inParams["RestorePointType"] = 12;
                        inParams["EventType"] = 100;
                        ManagementBaseObject outParams = sysRestore.InvokeMethod("CreateRestorePoint", inParams, null);
                        return (uint)outParams["ReturnValue"] == 0 ? "Başarılı!" : "Hata oluştu.";
                    }
                    catch (Exception ex) { return "Hata: " + ex.Message; }
                });
                MessageBox.Show(msg);
            }
            finally
            {
                btnGeriYuklemeYedegi.Enabled = true;
                btnGeriYuklemeYedegi.Text = "Oluştur";
                this.Cursor = Cursors.Default;
            }
        }

        private void btnAcilDurdur_Click(object sender, EventArgs e)
        {
            if (iptalKaynagi != null && !iptalKaynagi.IsCancellationRequested)
            {
                iptalKaynagi.Cancel();
            }
        }

        private async void btnCpuBenchmark_Click(object sender, EventArgs e)
        {
            ButonlariAyarla(false);
            btnAcilDurdur.Enabled = true;
            iptalKaynagi = new CancellationTokenSource();
            var token = iptalKaynagi.Token;
          
            lblBenchSkor.Text = "---";
            pbBenchmark.Style = ProgressBarStyle.Marquee;
            this.Cursor = Cursors.WaitCursor;

            long toplamIslem = 0;
            int testSuresi = 10;

            try
            {
                await Task.Run(() =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    Parallel.For(0, Environment.ProcessorCount, new ParallelOptions { CancellationToken = token }, (k) =>
                    {
                        Stopwatch coreSw = Stopwatch.StartNew();
                        while (coreSw.Elapsed.TotalSeconds < testSuresi)
                        {
                            if (token.IsCancellationRequested) break;

                            for (int i = 2; i < 10000; i++)
                            {
                                double junk = Math.Sqrt(i) * Math.Sin(i) * Math.Cos(i);
                            }
                            Interlocked.Add(ref toplamIslem, 10000);
                        }
                    });
                    sw.Stop();
                });

                pbBenchmark.Style = ProgressBarStyle.Blocks;
                pbBenchmark.Value = 100;

                if (!token.IsCancellationRequested)
                {
                    double skor = toplamIslem / 1000000.0;
                    string skorMetni = ((int)skor).ToString();

                    lblBenchSkor.Text = skorMetni;

                    GecmisGuncelle("CPU", skorMetni);                  
                }
            }           
            finally
            {
                ButonlariAyarla(true);
                btnAcilDurdur.Enabled = false; 
                this.Cursor = Cursors.Default;
            }
        }

        private async void btnDiskTest_Click(object sender, EventArgs e)
        {
            ButonlariAyarla(false);
            btnAcilDurdur.Enabled = true; 
            iptalKaynagi = new CancellationTokenSource();
            var token = iptalKaynagi.Token;

            lblDiskSonuc.Text = "Hazırlanıyor...";
            pbDisk.Value = 0;

            try
            {
                await Task.Run(() =>
                {
                    string file = Path.Combine(Path.GetTempPath(), "DoTe_Bench.tmp");
                    long size = 100 * 1024 * 1024;
                    byte[] data = new byte[64 * 1024]; new Random().NextBytes(data);

                    this.Invoke(new Action(() => lblDiskSonuc.Text = "Yazma Testi..."));
                    Stopwatch sw = Stopwatch.StartNew();
                    using (FileStream fs = new FileStream(file, FileMode.Create))
                    {
                        long total = 0;
                        while (total < size)
                        {
                            if (token.IsCancellationRequested) break; 
                            fs.Write(data, 0, data.Length);
                            total += data.Length;
                            this.Invoke(new Action(() => pbDisk.Value = (int)(total * 50 / size)));
                        }
                    }
                    sw.Stop();
                    double writeSpeed = (size / 1048576.0) / sw.Elapsed.TotalSeconds;

                    if (token.IsCancellationRequested) goto Bitir;

                    this.Invoke(new Action(() => lblDiskSonuc.Text = "Okuma Testi..."));
                    sw.Restart();
                    using (FileStream fs = new FileStream(file, FileMode.Open))
                    {
                        long total = 0;
                        while (total < size)
                        {
                            if (token.IsCancellationRequested) break;
                            fs.Read(data, 0, data.Length);
                            total += data.Length;
                            this.Invoke(new Action(() => pbDisk.Value = 50 + (int)(total * 50 / size)));
                        }
                    }
                    sw.Stop();
                    double readSpeed = (size / 1048576.0) / sw.Elapsed.TotalSeconds;

                    this.Invoke(new Action(() =>
                    {
                        string sonucMetni = $"Y: {writeSpeed:N0} MB/s | O: {readSpeed:N0} MB/s";
                        lblDiskSonuc.Text = sonucMetni;
                        pbDisk.Value = 100;
                        GecmisGuncelle("SSD", sonucMetni);
                    }));

                Bitir:
                    File.Delete(file);
                    if (token.IsCancellationRequested) this.Invoke(new Action(() => lblDiskSonuc.Text = "İptal Edildi."));
                });
            }
            catch { this.Invoke(new Action(() => lblDiskSonuc.Text = "Hata!")); }
            finally
            {
                ButonlariAyarla(true);
                btnAcilDurdur.Enabled = false; 
            }
        }

        private async void btnPuanHesapla_Click(object sender, EventArgs e)
        {
            btnPuanHesapla.Enabled = false;
            btnPuanHesapla.Text = "Hesaplanıyor...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                await Task.Run(() =>
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "winsat.exe");
                    if (!File.Exists(path) && Environment.Is64BitOperatingSystem)
                        path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative", "winsat.exe");

                    Process p = Process.Start(new ProcessStartInfo { FileName = path, Arguments = "formal", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = true, Verb = "runas" });
                    p.WaitForExit();
                });
                PuanlariGetir();
                MessageBox.Show("Puanlar Güncellendi!");
            }
            catch { MessageBox.Show("Hata oluştu."); }
            finally { btnPuanHesapla.Enabled = true; btnPuanHesapla.Text = "Puan Hesapla"; this.Cursor = Cursors.Default; }
        }

        private void linkTechnopat_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) { try { Process.Start("https://www.technopat.net/sosyal/"); } catch { } }
        private void linkDonanimArsivi_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) { try { Process.Start("https://forum.donanimarsivi.com/"); } catch { } }

        private void btnProgramKaldir_Click(object sender, EventArgs e)
        {
            if (lvProgramlar.CheckedItems.Count == 0)
            {
                MessageBox.Show("Lütfen kaldırmak için en az bir program seçin.", "Uyarı");
                return;
            }

            foreach (ListViewItem item in lvProgramlar.CheckedItems)
            {
                try
                {
                    string komut = item.Tag as string; 
                    if (!string.IsNullOrEmpty(komut))
                    {
                        if (komut.StartsWith("\"") && komut.IndexOf("\"", 1) > 0)
                        {
                            string exe = komut.Substring(1, komut.IndexOf("\"", 1) - 1);
                            string args = komut.Substring(komut.IndexOf("\"", 1) + 1).Trim();
                            Process.Start(exe, args);
                        }
                        else
                        {
                            string[] parcalar = komut.Split(new char[] { ' ' }, 2);
                            if (parcalar.Length > 1) Process.Start(parcalar[0], parcalar[1]);
                            else Process.Start(komut);
                        }
                    }
                    else
                    {
                        MessageBox.Show(item.Text + " için kaldırma bilgisi bulunamadı.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Kaldırma başlatılamadı: " + ex.Message);
                }
            }
        }
        private async void btnDosyaTara_Click(object sender, EventArgs e)
        {
            btnDosyaTara.Enabled = false;
            btnDosyaTara.Text = "Taranıyor... (Bekleyiniz)";
            lvBuyukDosyalar.Items.Clear();

            long secilenMB = (long)numMinBoyutMB.Value;

            long sinir = secilenMB * 1024 * 1024;

            await Task.Run(() =>
            {
                string baslangic = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                DosyaTara(baslangic, sinir);
            });

            btnDosyaTara.Enabled = true;
            btnDosyaTara.Text = "Diski Tara (Büyük Dosyaları Bul)";
        }

        private void btnDosyaSil_Click(object sender, EventArgs e)
        {
            if (lvBuyukDosyalar.CheckedItems.Count == 0) return;

            if (MessageBox.Show("Seçili dosyalar KALICI OLARAK silinecek. Emin misiniz?", "Dikkat", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                foreach (ListViewItem item in lvBuyukDosyalar.CheckedItems)
                {
                    try
                    {
                        string dosyaYolu = item.Tag.ToString();
                        File.Delete(dosyaYolu);
                        item.Remove();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Silinemedi: " + item.Text + "\n" + ex.Message);
                    }
                }
            }
        }

        private async void btnMedyaTara_Click(object sender, EventArgs e)
        {
            btnMedyaTara.Enabled = false;
            lvMedya.Items.Clear();

            await Task.Run(() =>
            {
                string baslangic = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                MedyaTara(baslangic);
            });

            btnMedyaTara.Enabled = true;
        }

        private void btnMedyaSil_Click(object sender, EventArgs e)
        {
            if (lvMedya.CheckedItems.Count == 0)
            {
                MessageBox.Show("Lütfen silinecek medyaları seçin.");
                return;
            }

            if (MessageBox.Show($"Seçili {lvMedya.CheckedItems.Count} medya dosyası KALICI OLARAK silinecek.\n\nEmin misiniz?", "Medya Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                int silinenSayisi = 0;
                foreach (ListViewItem item in lvMedya.CheckedItems)
                {
                    try
                    {
                        string dosyaYolu = item.Tag.ToString();
                        File.Delete(dosyaYolu);
                        item.Remove();
                        silinenSayisi++;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hata: {item.Text} silinemedi.\nSebep: {ex.Message}");
                    }
                }
            }
        }
    }
}
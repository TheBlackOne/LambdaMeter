using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Diagnostics;

namespace LambdaMeter
{
    [Flags]
    public enum Status
    {
        None = 0,
        CJ_Error = 1 << 0,
        Probe_Overheated = 1 << 1,
        Ubat_Low = 1 << 2,
        Ubat_High = 1 << 3,
        SPI_Error = 1 << 4,
        System_Ready = 1 << 5,
        Watchdog = 1 << 6,
        Calibration_Mode = 1 << 7
    }

    [Flags]
    public enum CJ
    {
        None = 0,
        VM1 = 1 << 0,
        VM2 = 1 << 1,
        UN1 = 1 << 2,
        UN2 = 1 << 3,
        IA_IP1 = 1 << 4,
        IA_IP2 = 1 << 5,
        DIAHD = 1 << 6,
        DIAHG = 1 << 7
    }

    public partial class Form1 : Form
    {
        SerialPort serialPort;

        public Form1()
        {
            InitializeComponent();
        }

        private void ReadComPorts()
        {
            string[] ports = SerialPort.GetPortNames();

            foreach (string port in ports)
            {
                comboBox1.Items.Add(port);
            }
        }

        private decimal GetReferenceVoltage(int reference)
        {
            decimal result = 5.0m / 1023.0m * reference;
            return Math.Round(result, 3);
        }

        private decimal GetBatteryVoltage(int ubat)
        {
            decimal result = 5.0m * 4.9m / 1023.0m * ubat;
            return Math.Round(result, 3);
        }

        private string GetStatusMessage(Status status)
        {
            string result = string.Empty;

            switch(status)
            {
                case Status.CJ_Error:
                    result = "CJ error!";
                    break;
                case Status.Probe_Overheated:
                    result = "Probe overheated!";
                    break;
                case Status.Ubat_Low:
                    result = "Ubat low!";
                    break;
                case Status.Ubat_High:
                    result = "Ubat high!";
                    break;
                case Status.SPI_Error:
                    result = "SPI error!";
                    break;
                case Status.System_Ready:
                    result = "System ready.";
                    break;
                case Status.Watchdog:
                    result = "Watchdog! Restart controller!";
                    break;
                case Status.Calibration_Mode:
                    result = "Calibration mode.";
                    break;
	        }

            return result;
        }

        private string GetCJErrorString(CJ cj)
        {
            string result = string.Empty;

            List<string> errorStrings = new List<string>();

            if (!cj.HasFlag(CJ.DIAHG) && !cj.HasFlag(CJ.DIAHD))
            {
                errorStrings.Add("Short to ground!");
            }
            else if (cj.HasFlag(CJ.DIAHD))
            {
                errorStrings.Add("Heating not connected!");
            }
            else if (cj.HasFlag(CJ.DIAHG))
            {
                errorStrings.Add("Short to Ubat!");
            }

            if ((!cj.HasFlag(CJ.VM1) && !cj.HasFlag(CJ.VM2)) ||
                (!cj.HasFlag(CJ.UN1) && !cj.HasFlag(CJ.UN2)) ||
                (!cj.HasFlag(CJ.IA_IP1) && !cj.HasFlag(CJ.IA_IP2)))
            {
                errorStrings.Add("Short to ground!");
            }
            else if ((cj.HasFlag(CJ.VM1) && !cj.HasFlag(CJ.VM2))
                   || (cj.HasFlag(CJ.UN1) && !cj.HasFlag(CJ.UN2))
                   || (cj.HasFlag(CJ.IA_IP1) && !cj.HasFlag(CJ.IA_IP2)))
            {
                errorStrings.Add("Battery weak!");
            }
            else if ((!cj.HasFlag(CJ.VM1) && cj.HasFlag(CJ.VM2))
                   || (!cj.HasFlag(CJ.UN1) && cj.HasFlag(CJ.UN2))
                   || (!cj.HasFlag(CJ.IA_IP1) && cj.HasFlag(CJ.IA_IP2)))
            {
                errorStrings.Add("Short to Ubat!");
            }

            result = string.Join(" ", errorStrings);

            return result;
        }

        private void ParseCSV(string csv)
        {
            string[] values = csv.Split(';');

            if (values.Count() == 5)
            {
                int lambda = int.Parse(values[0]);
                int reference = int.Parse(values[1]);
                int bat = int.Parse(values[2]);
                int status = int.Parse(values[3]);
                int cj = int.Parse(values[4]);

                decimal referenceVoltage = GetReferenceVoltage(reference);
                decimal batteryVoltage = GetBatteryVoltage(bat);

                string statusMessage = GetStatusMessage((Status)status);

                string cjError = string.Empty;
                if (cj < 255)
                {
                    cjError = GetCJErrorString((CJ)cj);
                }

                Debug.WriteLine(String.Format("lambda: {0}, reference: {1}, bat: {2}, status: {3}, cj: {4}", lambda, referenceVoltage, batteryVoltage, statusMessage, cjError));
            }
            else
            {
                // SendCSVModeCommand();
            }
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            ParseCSV(sp.ReadLine());
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ReadComPorts();
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            ReadComPorts();
        }

        private void openButton_Click(object sender, EventArgs e)
        {
            string serialPortName = comboBox1.SelectedItem.ToString();

            serialPort = new SerialPort(serialPortName);

            serialPort.BaudRate = 115200;
            serialPort.Parity = Parity.None;
            serialPort.StopBits = StopBits.One;
            serialPort.DataBits = 8;
            serialPort.Handshake = Handshake.None;
            serialPort.NewLine = "\r\n";
            serialPort.RtsEnable = true;

            serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            serialPort.Open();
        }

        private void fastButton_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.WriteLine("F");
            }
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.WriteLine("H");
            }
        }
    }
}

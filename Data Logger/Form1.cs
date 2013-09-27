using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace Data_Logger
{
    public partial class Form1 : Form
    {
        enum DataType {Speed, GX, GY, GZ, DForce, PForce, DFlex, PFlex };
        string[] DataTypes = new string[18] { "Pressure", "Baro 1", "Baro 2", "Baro 3", "Accel G", "Lat G", "Vert G", "DForce", "PForce", "DFlex", "PFlex", "Temp", "Temp 1", "Temp 2", "Temp 3", "Speed", "Lat", "Long" };
        // Speed, Roll, Pitch, Yaw, Accel Roll, Accel Pitch, Accel Yaw, D Flex, P Flex, D Force, P Force, Bar Press, Long, Lat
        double zoom = 2;
        string pathToFile = null;

        double[] mins = new double[16];
        double[] maxes = new double[16];
        double linesPerChart = 7.0;
        double startOffset = 0.0;
        bool autoscrollEnabled = false;

        System.IO.Ports.SerialPort serial; // Used for reading data from Arduino
        Queue<string> dataPoint = new Queue<string>();
        object queueLock = new object();

        // Textbox collections
        List<DataSet> dataSets = new List<DataSet>();

        public Form1()
        {
            InitializeComponent();

            chart1.Width = (int)((double)this.Width * .80);
            chart1.Height = this.Height - 100;
            dataGridView_main.Width = (int)((double)this.Width * .2);
            dataGridView_main.Location = new Point(chart1.Width + 30, chart1.Height / 2 - 140);
            dataGridView_main.Height = this.Height - 50;
            dataGridView_main.BackgroundColor = Color.White;
            dataGridView_main.ReadOnly = true;

            // Setup the chart
            chart1.BackColor = Color.White;

            Color[] chartColors = new Color[16] { 
                Color.Black,
                Color.Red, // roll
                Color.Green,
                Color.Blue, 
                Color.Red, // accel g
                Color.Green,
                Color.Blue, 
                Color.Red, // dForce
                Color.Blue, 
                Color.Red, // dFlex
                Color.Blue,
                Color.Black,
                Color.Red, 
                Color.Green,
                Color.Blue,
                Color.Purple,
            };

            // Add legends to the graph
            for (int i = 1; i < 6; i++)
            {
                if (i != 1) chart1.Legends.Add("Legend" + i.ToString()); // Legend1 is added by default
            }
            int ypos = 1;
            for (int i = 0; i < chart1.Legends.Count; i++)
            {
                switch (i)
                {
                    case 0: chart1.Legends[i].Title = "Baro"; break;
                    case 1: chart1.Legends[i].Title = "G Force"; break;
                    case 2: chart1.Legends[i].Title = "Force"; break;
                    case 3: chart1.Legends[i].Title = "Flex"; break;
                    case 4: chart1.Legends[i].Title = "Temp"; break;
                }
                chart1.Legends[i].Position = new System.Windows.Forms.DataVisualization.Charting.ElementPosition(1, ypos, 7, 15);
                ypos += 20;
                chart1.Legends[i].BackColor = Color.White;
            }

            int position = 0;
            // Add all the areas to the chart
            for (int i = 1; i < 6; i++)
            {
                string areaName = "Area" + i.ToString();
                chart1.ChartAreas.Add(areaName);
                chart1.ChartAreas[areaName].AxisX.Interval = .5;

                if (i == 1) chart1.ChartAreas[areaName].AxisY.Interval = 5;
                else if (i == 2) chart1.ChartAreas[areaName].AxisY.Interval = .1;
                
                else if (i == 3) chart1.ChartAreas[areaName].AxisY.Interval = 10;
                
                else if (i == 4) chart1.ChartAreas[areaName].AxisY.Interval = .1;
                else chart1.ChartAreas[areaName].AxisY.Interval = 1;
                chart1.ChartAreas[areaName].Position.X = 7;
                chart1.ChartAreas[areaName].Position.Y = position;
                position += 20;

                chart1.ChartAreas[areaName].Position.Height = 20;
                chart1.ChartAreas[areaName].Position.Width = 93;
            }

            // Add data series to graphs
            int colorNumber = 0;
            for (int i = 0; i < DataTypes.Length - 2; i++)
            {
                chart1.Series.Add(DataTypes[i]);

                if (i >= 0 && i < 4)
                {
                    chart1.Series[DataTypes[i]].ChartArea = "Area1";
                    chart1.Series[DataTypes[i]].Legend = "Legend1";
                }
                else if (i >= 4 && i <= 6)
                {
                    chart1.Series[DataTypes[i]].ChartArea = "Area2";
                    chart1.Series[DataTypes[i]].Legend = "Legend2";
                }
                else if (i == 7 || i == 8)
                {
                    chart1.Series[DataTypes[i]].ChartArea = "Area3";
                    chart1.Series[DataTypes[i]].Legend = "Legend3";
                }
                else if (i == 9 || i == 10)
                {
                    chart1.Series[DataTypes[i]].ChartArea = "Area4";
                    chart1.Series[DataTypes[i]].Legend = "Legend4";
                }
                else
                {
                    chart1.Series[DataTypes[i]].ChartArea = "Area5";
                    chart1.Series[DataTypes[i]].Legend = "Legend5";
                }

                chart1.Series[DataTypes[i]].Color = chartColors[colorNumber++];
                chart1.Series[DataTypes[i]].BorderWidth = 2;
                chart1.Series[DataTypes[i]].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            }

            // Try to load the previously used file using the data.dat file
            try
            {
                StreamReader sr = new StreamReader("data.dat");
                pathToFile = sr.ReadLine();
                if (pathToFile != null && pathToFile != "")
                {
                    loadFile(pathToFile);
                }
                sr.Close();
            }
            catch (FileNotFoundException)
            {
                // Continue on...
            }

            // Setup event handlers for Serial menu
            serialToolStripMenuItem.DropDownOpening += new EventHandler(serialMenu_Click);
            serialToolStripMenuItem.DropDownItemClicked += new ToolStripItemClickedEventHandler(serialToolStripItem_Click);
        }

        /// <summary>
        /// Refresh the list of available COM ports when the user clicks the "Serial" menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void serialMenu_Click(object sender, EventArgs e)
        {
            serialToolStripMenuItem.DropDownItems.Clear();
            string[] comPorts = System.IO.Ports.SerialPort.GetPortNames();
            if (comPorts.Length > 0)
            {
                foreach (string port in comPorts)
                {
                    serialToolStripMenuItem.DropDownItems.Add(port);
                }
            }
            else
            {
                serialToolStripMenuItem.DropDownItems.Add("No COM ports available");
                serialToolStripMenuItem.DropDownItems[0].Enabled = false;
            }
        }

        /// <summary>
        /// Open the requested COM port when the user clicks an item in the "Serial" menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void serialToolStripItem_Click(object sender, ToolStripItemClickedEventArgs e)
        {
            serial = new System.IO.Ports.SerialPort(e.ClickedItem.Text, 9600);
            ToolStripMenuItem item = (ToolStripMenuItem)sender;

            try
            {
                serial.Open();
                
                item.CheckOnClick = true;
                item.Checked = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            // Uncheck all other items in menu
            foreach (var menuItem in serialToolStripMenuItem.DropDown.Items)
            {
                ToolStripMenuItem menuItem2 = (ToolStripMenuItem)menuItem;
                if (menuItem2 != item)
                {
                    menuItem2.Checked = false;
                }
            }
        }

        /// <summary>
        /// Load a file via the File->Open button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.ShowDialog();
            if (d.FileName == null || d.FileName == "") return;

            loadFile(d.FileName);

        }

        /// <summary>
        /// Load the given file and fill the chart
        /// </summary>
        /// <param name="pathToFile"></param>
        private void loadFile(string pathToFile)
        {
            int count = 0;
            int columnCount = 0; // Number of columns of data in file
            double startTime = -1;
            double endTime = -1;
            double[] min = new double[DataTypes.Length];
            for (int i = 0; i < min.Length; i++) min[i] = double.MaxValue;
            double[] averages = new double[DataTypes.Length];
            double[] total = new double[DataTypes.Length];
            double[] max = new double[DataTypes.Length];
            
            foreach (System.Windows.Forms.DataVisualization.Charting.Series chartSeries in chart1.Series)
            {
                chartSeries.Points.Clear();
            }

            try
            {
                using (StreamReader sr = new StreamReader(pathToFile))
                {
                    while (!sr.EndOfStream) // Read all the lines
                    {
                        string[] elements = sr.ReadLine().Split(' ');
                        double[] numbers = new double[elements.Length];
                        for (int i = 0; i < elements.Length; i++)
                        {
                            numbers[i] = Math.Round(Convert.ToDouble(elements[i]), 1); // Convert everything to numebrs
                        }
                        columnCount = numbers.Length;

                        int index = 1;
                        for (int i = 0; i < columnCount - 3; i++) // Add points to graph
                        {
                            chart1.Series[DataTypes[i]].Points.AddXY(numbers[0], numbers[index++]);
                        }

                        for (int i = 0; i < columnCount - 3; i++) // Keep track of totals
                        {
                            total[i] += numbers[i + 1];
                        }
                        count++;

                        for (int i = 0; i < columnCount - 3; i++) // Keep track of mins and maxes
                        {
                            if (numbers[i + 1] < min[i]) min[i] = numbers[i + 1];
                            if (numbers[i + 1] > max[i]) max[i] = numbers[i + 1];
                        }

                        if (startTime == -1) startTime = Convert.ToDouble(elements[0]);
                        endTime = Convert.ToDouble(elements[0]);
                    }
                    this.pathToFile = pathToFile;
                }

                // Create data sets for data grid
                for (int i = 0; i < columnCount - 1; i++) // Subtract 2 for lat and long
                {
                    DataSet d = new DataSet()
                    {
                        Type = DataTypes[i],
                        Value = 0,
                        Min = min[i],
                        Avg = Math.Round(total[i] / (double)count, 4),
                        Max = max[i]
                    };
                    dataSets.Add(d);
                }
                dataGridView_main.DataSource = dataSets;

                dataGridView_main.RowHeadersVisible = false;
                dataGridView_main.ColumnHeadersVisible = true;
                dataGridView_main.RowHeadersWidth = 75;

                dataGridView_main.Columns[1].Width = dataGridView_main.Width / 5;
                dataGridView_main.Columns[2].Width = dataGridView_main.Width / 5;
                dataGridView_main.Columns[3].Width = dataGridView_main.Width / 5;
                dataGridView_main.Columns[4].Width = dataGridView_main.Width / 5;
                dataGridView_main.Columns[0].Width = dataGridView_main.Columns[0].Width - 45;
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("Previously used file not found. Load a new one.");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            for (int i = 1; i < 6; i++)
            {
                var ca = chart1.ChartAreas["Area" + i.ToString()];
                ca.AxisX.Minimum = 0.0;
                if (i == 1)
                {
                    double minValue = getMin(min, 0, 3);
                    double maxValue = getMax(max, 0, 3);
                    ca.AxisY.Minimum = minValue * .95;
                    ca.AxisY.Maximum = maxValue * 1.05;
                    double diff = Math.Round((maxValue - minValue) / linesPerChart, 1);
                    ca.AxisY.Interval = diff;
                }
                else if (i == 2)
                {
                    double minValue = getMin(min, 4, 6);
                    double maxValue = getMax(max, 4, 6);
                    ca.AxisY.Minimum = minValue * .95;
                    ca.AxisY.Maximum = maxValue * 1.05;
                    double diff = Math.Round((maxValue - minValue) / linesPerChart, 1);
                    ca.AxisY.Interval = diff;
                }
                else if (i == 3)
                {
                    double minValue = getMin(min, 7, 8);
                    double maxValue = getMax(max, 7, 8);
                    ca.AxisY.Minimum = minValue * .95;
                    ca.AxisY.Maximum = maxValue * 1.05;
                    double diff = Math.Round((maxValue - minValue) / linesPerChart, 1);
                    ca.AxisY.Interval = diff;
                }
                else if (i == 4)
                {
                    double minValue = getMin(min, 9, 10);
                    double maxValue = getMax(max, 9, 10);
                    ca.AxisY.Minimum = minValue * .95;
                    ca.AxisY.Maximum = maxValue * 1.05;
                    double diff = Math.Round((maxValue - minValue) / linesPerChart, 1);
                    ca.AxisY.Interval = diff;
                }
                else if (i == 5)
                {
                    double minValue = getMin(min, 11, 15);
                    double maxValue = getMax(max, 11, 15);
                    ca.AxisY.Minimum = minValue * .95;
                    ca.AxisY.Maximum = maxValue * 1.05;
                    double diff = Math.Round((maxValue - minValue) / linesPerChart, 1);
                    ca.AxisY.Interval = diff;
                }

                ca.AxisX.ScaleView.Zoomable = true;
                ca.AxisX.ScaleView.Zoom((int)startTime, (int)startTime + zoom); // Chart shows range of 4 seconds
                if (i == 5) ca.AxisX.ScrollBar.ButtonStyle = System.Windows.Forms.DataVisualization.Charting.ScrollBarButtonStyles.SmallScroll;
                else ca.AxisX.ScrollBar.Enabled = false;
            }

            this.Text = "Data Logger - " + Path.GetFileName(pathToFile);
        }

        double getMin(double[] array, int start, int end)
        {
            double min = double.MaxValue;
            for (int i = start; i <= end; i++)
            {
                if (array[i] < min) min = array[i];
            }
            return min;
        }
        double getMax(double[] array, int start, int end)
        {
            double max = 0;
            for (int i = start; i <= end; i++)
            {
                if (array[i] > max) max = array[i];
            }
            return max;
        }

        /// <summary>
        /// Clicking the GO button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_go_Click(object sender, EventArgs e)
        {
            double enteredValue;
            if (Double.TryParse(textbox_time.Text, out enteredValue))
            {
                if (enteredValue < 0 || enteredValue > chart1.Series[this.DataTypes[0]].Points[chart1.Series[this.DataTypes[0]].Points.Count - 1].XValue)
                {
                    MessageBox.Show("Time is out of range of the loaded data.");
                    return;
                }

                // Find closest data point
                int closestIndex = 0;
                double closestTime = Double.MaxValue;
                for (int i = 0; i < chart1.Series[0].Points.Count; i++)
                {
                    double temp = Math.Abs(chart1.Series[0].Points[i].XValue - enteredValue);
                    if (temp < closestTime)
                    {
                        closestIndex = i;
                        closestTime = temp;
                    }
                }

                // Update scroll positions on charts
                var ca = chart1.ChartAreas["Area5"].AxisX.ScaleView;
                double value = chart1.Series[0].Points[closestIndex].XValue;
                ca.Zoom(value - 2.0, value + 2.0);
                for (int i = 1; i < 5; i++)
                {
                    chart1.ChartAreas["Area" + i.ToString()].AxisX.ScaleView.Zoom(ca.ViewMinimum, ca.ViewMaximum);
                }

                // Update values in "Value" column
                for (int i = 0; i < DataTypes.Length - 2; i++) // Minus 2 to exclude lat and long
                {
                    if (chart1.Series[i].Points.Count > 0)
                    {
                        dataGridView_main[1, i].Value = chart1.Series[i].Points[closestIndex].YValues[0];
                    }
                }
            }
            else
            {
                MessageBox.Show("Invalid value entered");
            }
        }

        // Enter is pressed while in the textbox
        private void textbox_time_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                button_go.PerformClick();
            }
        }

        // Window is closed
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StreamWriter sw = new StreamWriter("data.dat", false); // Always overwrites file
            sw.WriteLine(pathToFile);
            sw.Close();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            chart1.Width = (int)((double)this.Width * .80);
            chart1.Height = this.Height - 100;
            dataGridView_main.Width = (int)((double)this.Width * .19);
            dataGridView_main.Location = new Point(chart1.Width + 30, chart1.Height / 2 - 140);
            dataGridView_main.Height = this.Height - 50;
        }

        // Fired when scroll bar is used
        private void chart1_AxisViewChanged(object sender, System.Windows.Forms.DataVisualization.Charting.ViewEventArgs e)
        {
            var ca = chart1.ChartAreas["Area5"].AxisX.ScaleView;
            for (int i = 1; i < 5; i++)
            {
                chart1.ChartAreas["Area" + i.ToString()].AxisX.ScaleView.Zoom(ca.ViewMinimum, ca.ViewMaximum);
            }
        }

        
        private void button_clear_Click(object sender, EventArgs e)
        {
            foreach (System.Windows.Forms.DataVisualization.Charting.Series chartSeries in chart1.Series)
            {
                chartSeries.Points.Clear();
            }
        }

        private void button_play_Click(object sender, EventArgs e)
        {
            if (button_play.Text == "Play")
            {
                if (this.serial != null && this.serial.IsOpen)
                {
                    this.serial.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(serialDataReceived);
                }
                else
                {
                    MessageBox.Show("Serial port not opened. Select one from 'Serial' menu");
                    return;
                }

                button_clear.PerformClick();
                button_play.Text = "Stop";
            }
            else
            {
                serial.Dispose();
                button_play.Text = "Play";
            }
        }

        private void serialDataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            string line = serial.ReadLine();
            Console.WriteLine(line);
            lock(queueLock)
            {
                dataPoint.Enqueue(line);
            }
            this.BeginInvoke(new MethodInvoker(addDataPoints)); // Add data points on main GUI thread
        }

        /// <summary>
        /// Add queued data points to the charts
        /// </summary>
        private void addDataPoints()
        {
            while (dataPoint.Count > 0)
            {
                lock (queueLock)
                {
                    string line = dataPoint.Dequeue();
                    string[] elements = line.Split(' ');
                    double[] numbers = new double[elements.Length];
                    if (elements.Length > 0)
                    {
                        for (int i = 0; i < elements.Length; i++)
                        {
                            if (!Double.TryParse(elements[i], out numbers[i]))
                            {
                                Console.WriteLine("Crap found: " + elements[i]);
                            }
                            else Console.Write(elements[i] + " ");
                        }
                        Console.WriteLine();

                        if (startOffset == 0.0)
                        {
                            startOffset = numbers[0];
                            Console.WriteLine("start offset " + startOffset);
                        }

                        int index = 1;
                        for (int i = 0; i < numbers.Length - 1; i++) // Add points to graph
                        {
                            chart1.Series[DataTypes[i]].Points.AddXY(numbers[0] - startOffset, numbers[index++]);
                        }

                        if (autoscrollEnabled)
                        {
                            foreach (var chartArea in this.chart1.ChartAreas)
                            {
                                chartArea.AxisX.ScaleView.Scroll(numbers[0] - zoom);
                            }
                        }
                    }
                }
            }
        }

        private void newWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("Data Logger.exe");
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            autoscrollEnabled = !autoscrollEnabled;
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }


    // Represents a set of data for a specific time
    public class DataSet
    {
        public string Type { get; set; }
        public double Value { get; set; }
        public double Min { get; set; }
        public double Avg { get; set; }
        public double Max { get; set; }
    }
}

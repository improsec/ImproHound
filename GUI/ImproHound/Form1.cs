using System;
using System.Windows.Forms;

namespace ImproHound
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private async void button1_ClickAsync(object sender, EventArgs e)
        {
            DBConnection greeter = new DBConnection("bolt://localhost:7687", "neo4j", "bloodhound");

            await greeter.asyncAsync("CREATE (a:Greeting) SET a.message = \"hi\" RETURN a.message");
        }
    }
}

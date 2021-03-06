﻿//Auteur : JMY
//Date   : 09.5.2019 
//Lieu   : ETML
//Descr. : importateur sql ->access

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ADOX;

namespace Dbmain2AccessImport
{
    public partial class Form1 : Form
    {
        private List<string> importStatements = new List<string>();

        Dictionary<string, string> typeReplacement = new Dictionary<string, string>();

        public Form1()
        {
            InitializeComponent();

            typeReplacement.Add("-- Sequence attribute not implemented --", "autoincrement");
            typeReplacement.Add("short", "long");
            typeReplacement.Add("\u001f", "");//strange underscore found...
            typeReplacement.Add("_[0-1]", "");//mld info generated by dbmain
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            ofdSource.Filter = "sql files (*.sql)|*.sql|All files (*.*)|*.*";
            DialogResult result = ofdSource.ShowDialog();

            if (result == DialogResult.OK)
            {
                txtSource.Text = ofdSource.FileName;
                btnAnalyser_Click(null, null);
            }
        }

        private void btnAnalyser_Click(object sender, EventArgs e)
        {
            importStatements.Clear();
            pnlResult.Controls.Clear();


            if (txtSource.Text != "")
            {
                string[] lines = File.ReadAllLines(ofdSource.FileName);

                string content = "";
                //clean comments and empty lines
                foreach (string line in lines)
                {
                    if (line.StartsWith("--") || line.StartsWith("\n") || line == "")
                    {
                        //useless
                    }
                    else
                    {
                        content += line;
                    }
                }

                foreach (KeyValuePair<string,string> pair in typeReplacement)
                {
                    content = content.Replace(pair.Key, pair.Value);
                }

                string[] statements = content.Split(';');

                int currentX, currentY;
                currentX = pnlResult.Left+15;
                currentY = 0;//relatif au panel dans lequel il est ajouté

                for (int statementCount = 0; statementCount < statements.Length - 1; statementCount++)
                {
                    string statement = statements[statementCount];
                    Label label = new Label();

                    label.Location = new Point(currentX, currentY);
                    label.Text = String.Join(" ", statement.Split(' ').Take(3))+" ...";
                    label.Width = 250;

                    CheckBox checkBox = new CheckBox();
                    checkBox.Location = new Point(currentX + label.Width + 7, currentY);
                    checkBox.Checked = IsConsideredACompatibleStatement(statement);
                    checkBox.Tag = "cb-" + statementCount;

                    importStatements.Add(statement);


                    this.pnlResult.Controls.Add(label);
                    this.pnlResult.Controls.Add(checkBox);


                    this.Refresh();
                    currentY += checkBox.Height + 5;

                }

                if (statements.Length > 1)
                    btnImport.Enabled = true;

            }
        }

        private Boolean IsConsideredACompatibleStatement(string statement)
        {
            string lstatement = statement.ToLower();
            return lstatement.StartsWith("create") || lstatement.StartsWith("alter");
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            //Filter checked statements
            List<string> selectedStatements = new List<string>();
            for (int statementCount = 0; statementCount < importStatements.Count; statementCount++)
            {
                foreach (CheckBox cb in pnlResult.Controls.OfType<CheckBox>())
                {
                    if (cb.Checked && Convert.ToInt32(cb.Tag.ToString().Split('-')[1]) == statementCount)
                    {
                        selectedStatements.Add(importStatements[statementCount]);
                        break;
                    }
                }
            }

            string tempFile = CreateDb();
            List<string> errors = ExecuteNonQuery(selectedStatements.ToArray(), tempFile);

            if (errors.Count == 0)
            {
                sfdMdb.Filter = "mdb files (*.mdb)|*.mdb|All files (*.*)|*.*";
                sfdMdb.CheckPathExists = false;
                sfdMdb.Title = "Fichier MDB destination";
                sfdMdb.FileName = Path.GetFileNameWithoutExtension(ofdSource.FileName)+".mdb";
                DialogResult result = sfdMdb.ShowDialog();
                

                Console.WriteLine("Save path : "+sfdMdb.FileName);
                if (result == DialogResult.OK)
                {
                    if (File.Exists(sfdMdb.FileName))
                    {
                        File.Delete(sfdMdb.FileName);
                    }
                    //sfdMdb.
                    File.Move(tempFile, sfdMdb.FileName);
                }
            }
            else
            {
                MessageBox.Show(this,"Les requêtes suivantes ont échoué (vous pouvez réessayer en décochant les instructions problématiques) :\n\n"+String.Join("\n\n",errors.ToArray()), "Erreurs");
            }
        }

        public string CreateDb()
        {
            ADOX.Catalog cat = new ADOX.Catalog();
            string tempFile = Path.GetTempFileName();
            File.Delete(tempFile);

            cat.Create(BuildQueryString(tempFile));
            cat.ActiveConnection.Close();

            return tempFile;
        }

        private static string BuildQueryString(string file)
        {
            return "Provider=Microsoft.Jet.OLEDB.4.0;" + "Data Source=" + file + ";" + "Jet OLEDB:Engine Type=5";
        }

        public List<string> ExecuteNonQuery(string[] sqls, string accessFile)
        {
            List<string> errors = new List<string>();

            OleDbConnection conn = null;
            try
            {
                conn = new OleDbConnection(BuildQueryString(accessFile));
                conn.Open();

                foreach (string sql in sqls)
                {
                    OleDbCommand cmd = new OleDbCommand(sql, conn);
                    try
                    {
                        Console.WriteLine(sql);
                        cmd.ExecuteNonQuery();
                    }
                    catch(OleDbException e)
                    {
                        errors.Add(e.Message + " (sql=" + sql+")");
                    }
                }

            }
            catch (Exception e)
            {
                errors.Add(e.Message);
            }
            finally
            {
                if (conn != null) conn.Close();
            }

            return errors;
        }

    }
}

using System;

using System.Windows;
using System.Drawing;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Diagnostics;

//using java.io;
//using java.util;
using edu.stanford.nlp.ling;
using edu.stanford.nlp.tagger.maxent;
using System.Collections.Generic;
using static Osion.Lib;
using System.Windows.Controls;
using System.Linq;
using System.IO;

namespace Osion
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
       
        private static MaxentTagger tagger;
        IDictionary<string, string[]> cfg = new Dictionary<string, string[]>()
                                            {
                                               {"S"   , new string[]{"VP","NP2"} },
                                               {"VP"  , new string[]{"VP1 TP NP","vbn nn to nn"} },
                                               {"TP"  , new string[]{"to" ,"in", ""} },
                                               {"VP1" , new string[]{"vb","jj"} },
                                               {"NP"  , new string[]{"NP1","dt NP1",""} },
                                               {"NP1" , new string[]{"NP2 NP3"} },
                                               {"NP2" , new string[]{"nn","nns","nnp"} },
                                               {"NP3" , new string[]{"NP2","vbn NP2",""} }
                                            };

        Stack<DEF> nonterminals = new Stack<DEF>();
        Stack<string> terminals = new Stack<string>();
        Queue<string> quoted_data_values = new Queue<string>();
        Queue<string> unquoted_data_values = new Queue<string>();
        IDictionary<string, Function<DEF,DEF>> functions = new Dictionary<string, Function<DEF,DEF>>();
        IDictionary<string, string> variables = new Dictionary<string, string>();

        Queue<string> inputQueue = new Queue<string>();

        private static string[] tokens;
        private static string[] lexemes;
        private static int index;
        private static bool error;
        private static string l;
        private static string RESULT_CODE;
        private static ProcessStartInfo startInfo;
        private static Process process;
        private static string USER_DIR = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static bool enterIsOn = false;
        private List<string> commands_data = File.ReadAllLines("commands.list").ToList();

        public MainWindow()
        {
            // system initialization, you can ignore
            InitializeComponent();
            //new Popup("Dude ! that's an error ?!", "Error").Show();
            //return;

            // load saved data
            string[] variables_lines = File.ReadAllLines("variables.map");
            foreach (string line in variables_lines)
            {
                string[] split_line = line.Split(new[] { ':' }, 2);
                variables[split_line[0]] = split_line[1];
            }

            // start the server
            Server server = new Server();
            server.onMessage(delegate (string message)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    processSentence(message);
                });
            });
            server.Start();

            // create a process
            startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            process = new Process();

            // set working directory
            startInfo.WorkingDirectory = USER_DIR;
            changeProcessDirectory(@"F:\Mini Project");
            process.StartInfo = startInfo;

            
            
            // add tray icon
            System.Windows.Forms.ContextMenu contextMenu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem menuItem = new System.Windows.Forms.MenuItem();
            contextMenu.MenuItems.AddRange(
                    new System.Windows.Forms.MenuItem[] { menuItem });

            menuItem.Index = 0;
            menuItem.Text = "Exit";
            menuItem.Click += delegate (object sender, EventArgs e)
            {
                this.Close();
            };

            // set tray icon
            System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
            ni.Icon = new System.Drawing.Icon(@"..\..\Resources\oston.ico");
            ni.ContextMenu = contextMenu;
            ni.Visible = true;
            ni.Click += delegate (object sender, EventArgs args)
            {
                if(this.WindowState == WindowState.Minimized)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    animate(this, OpacityProperty, 0, 1, 200);
                }
                
            };

            // settings done , start the core part
            initialize();
            
        }

        protected override void OnStateChanged(EventArgs e)
        {
           /* if(WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
            base.OnStateChanged(e);*/
        }

        private void execute(string command) {
            //var list = command.Split(new[] {' '},2);

            //Console.WriteLine(list[0]);
            startInfo.FileName = "cmd.exe";//list[0];
            //changeProcessDirectory(@"F:\Mini Project");
            startInfo.Arguments = "/C "+command;
            //if (list.Length != 1)
            //{
            //    startInfo.Arguments = "/C chrome google.com";// list[1];
            //}
            process.StartInfo = startInfo;
                  
            process.Start();
        }

        private void initialize()
        {
            foreach(var key in cfg)
            {
                foreach(var product in key.Value)
                {
                    functions[key.Key + ":" + product] = delegate (DEF nt)
                    {
                        foreach(var item in product.Split(' '))
                        {
                            if(item.Length != 0 && isNonTerminal(item))
                            {
                                nonterminals.Pop();
                            }else if(item.Length != 0 )
                            {
                                terminals.Pop();
                            }
                        }
                        return nt;
                    };
                }
            }

            functions["NP3:"] = delegate (DEF NP3)
            {
                ///////
                NP3.type = "NULL";
                ///////

                return NP3;
            };

            functions["NP3:vbn NP2"] = delegate (DEF NP3)
            {
                var NP2 = nonterminals.Pop();
                terminals.Pop();
                ///////
                NP3.value = NP2.value;
                ///////

                return NP3;
            };

            functions["NP3:NP2"] = delegate (DEF NP3)
            {
                var NP2 = nonterminals.Pop();
                ///////
                NP3.value = NP2.value;
                ///////

                return NP3;
            };

            functions["NP2:nnp"] = delegate (DEF NP2)
            {
                var nnp = terminals.Pop();
                ///////
                NP2.value = nnp;
                ///////

                return NP2;
            };

            functions["NP2:nns"] = delegate (DEF NP2)
            {
                var nns = terminals.Pop();
                ///////
                NP2.value = nns;
                ///////

                return NP2;
            };

            functions["NP2:nn"] = delegate (DEF NP2)
            {
                var nn = terminals.Pop();
                ///////
                NP2.value = nn;
                ///////

                return NP2;
            };

            functions["NP1:NP2 NP3"] = delegate (DEF NP1)
            {
                var NP3 = nonterminals.Pop();
                var NP2 = nonterminals.Pop();
                ///////

                if (NP3.type == "NULL" || NP3.value == "directory" || NP3.value == "file")
                {
                    NP1.value = NP2.value;
                    NP1.type = NP3.value.ToUpper();
                }
                else if (NP2.value == "directory" || NP2.value == "file")
                {
                    NP1.value = NP3.value;
                    NP1.type = NP2.value.ToUpper();
                }
                
                ///////

                return NP1;
            };

            functions["NP:NP1"] = delegate (DEF NP)
            {
                var NP1 = nonterminals.Pop();

                //////////////

                NP.value = NP1.value;
                //Console.WriteLine("the val : " + NP1.value);
                if (NP1.type == "DIRECTORY" || NP1.type == "FILE")
                {
                    NP.type = NP1.type;
                }
                else if (!isPATH(NP1.value) && isURL(NP1.value))
                {
                    if (!NP1.value.StartsWith("http://") && !NP1.value.StartsWith("https://"))
                        NP.value = "http://" + NP1.value;
                    NP.type = "URL";
                }
               
                
               
                /////////////
                
                return NP;
            };

            functions["NP:dt NP1"] = delegate (DEF NP)
            {
                var NP1 = nonterminals.Pop();
                terminals.Pop();
                //////////////

                NP.value = NP1.value;
                //Console.WriteLine("the val : " + NP1.value);
                if (NP1.type == "DIRECTORY" || NP1.type == "FILE")
                {
                    NP.type = NP1.type;
                }
                else if (!isPATH(NP1.value) && isURL(NP1.value))
                {
                    if (!NP1.value.StartsWith("http://") && !NP1.value.StartsWith("https://"))
                        NP.value = "http://" + NP1.value;
                    NP.type = "URL";
                }

                /////////////

                return NP;
            };

            functions["NP:"] = delegate (DEF NP)
            {
                
                /////////////
                NP.type = "NULL";
                /////////////

                return NP;
            };


            functions["VP1:jj"] = delegate (DEF VP1)
            {
                var jj = terminals.Pop();
                ///////
                VP1.value = jj;
                ///////

                return VP1;
            };

            functions["VP1:vb"] = delegate (DEF VP1)
            {
                var vb = terminals.Pop();
                ///////
                VP1.value = vb;
                ///////

                return VP1;
            };

            functions["TP:"] = delegate (DEF TP)
            {
                return TP;
            };

            functions["TP:to"] = delegate (DEF TP)
            {
                terminals.Pop();
                ///////
                TP.value = "to";
                ///////

                return TP;
            };

            functions["VP:vbn nn to nn"] = delegate (DEF VP)
            {
                var n2 = terminals.Pop();
                terminals.Pop();
                var n1 = terminals.Pop();
                var vbn = terminals.Pop();
                ///////
                if (vbn == "set")
                {

                    variables[n1] = n2.Trim();
                    new Popup("Mapped " + n1 + " to " + n2.Trim(), "Error").Show();
                    string save_data = "";
                    foreach (string key in variables.Keys.ToArray())
                    {
                        save_data += key + ":" + variables[key] + "\n";
                    }
                    File.WriteAllText("variables.map", save_data);

                }
                ///////

                return VP;
            };

            functions["VP:VP1 TP NP"] = delegate (DEF VP)
            {
                var NP = nonterminals.Pop();
                var TP = nonterminals.Pop();
                var VP1 = nonterminals.Pop();

                ///////

                if (NP.type == "NULL")
                {
                    if (VP1.value == "restart")
                    {
                        VP.code = "shutdown /r";
                    }
                    
                }
                else if (VP1.value == "scan" && NP.value == "virus")
                {
                    VP.code = "\"C:\\Program Files\\Windows Defender\\MpCmdRun.exe\" -scan -1";
                    new Popup("Virus Scan initiated! I'll notify you when its done", "Error").Show();
                }
                else if (VP1.value == "go" && TP.value == "to")
                {
                    changeProcessDirectory(NP.value);
                }
                else if (VP1.value == "create" || VP1.value == "make" || VP1.value == "generate")
                {

                    if (NP.type == "FILE" || (isPATH(NP.value) && NP.type != "DIRECTORY"))
                    {
                        VP.code = $"echo .> \"{NP.value}\"";
                    }
                    else
                    {
                        VP.code = $"mkdir \"{NP.value}\" ";
                    }
                }
                else if (VP1.value == "open" || VP1.value == "start" || VP1.value == "run" || VP1.value == "execute")
                {
                    VP.code = "start " + NP.value;
                }
                else if (VP1.value == "uninstall")
                {
                    VP.code = "control appwiz.cpl";
                }

                Console.WriteLine("code : " + VP.code);
                ///////

                return VP;
            };

            functions["S:NP2"] = delegate (DEF S)
            {
                var NP2 = nonterminals.Pop();

                if (NP2.value == "shutdown")
                {
                    RESULT_CODE = "shutdown /s";
                }
                else if (NP2.value == "sleep") {
                    //Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
                    RESULT_CODE = $"\"{AppDomain.CurrentDomain.BaseDirectory}psshutdown.exe\" -d -t 0 -accepteula";
                }
                else if (NP2.value == "uninstall")
                {
                    RESULT_CODE = "control appwiz.cpl";
                }
                else
                {
                    //Console.WriteLine(NP2.value);
                    processSentence(NP2.value);
                    RESULT_CODE = "";
                }

                ///////

                return S;
            };

            functions["S:VP"] = delegate (DEF S)
            {
                var VP = nonterminals.Pop();

                RESULT_CODE = VP.code;   
                ///////

                return S;
            };

            
            var str = @" set var = ""g:\j.a.r.v.i.s\hello there my friend\yes\g\file.exe"" and ""http://www.google.com"" ";
            //Console.WriteLine(str.Replace(Regex.Match(str, "\"(.*?)\"").ToString(),"<QUOTE>"));

            //var str = @" set var = ""g:\j.a.r.v.i.s\hello there my friend\yes\g\file.exe"" and yeah ";
            //Console.WriteLine(str.Replace(Regex.Match(str, "\"(.*?)\"").ToString(), "<QUOTE>"));

            

            var model = @"../../../../stanford_pos_tagger_data/models/" + "english-bidirectional-distsim.tagger";
            //Console.WriteLine(System.AppContext.BaseDirectory);
            if (!System.IO.File.Exists(model))
                throw new Exception($"Check path to the model file '{model}'");
            tagger = new MaxentTagger(model);

        }

        private void changeProcessDirectory(string path) {
       
            startInfo.WorkingDirectory = path;
            MessageBox.Show("Changed to "+startInfo.WorkingDirectory);
        }

        private void parse(string input)
        {
            Console.WriteLine("input : " + input);
            tokens = (input+" $").Split(' ');
            
            index = 0;
            error = false;
            l = nexttoken();

            try
            {
                evaluate("S");
            }catch(Exception ex)
            {
                
            }
            if (l == "$" && !error)
            {
                Console.WriteLine("Parse Success");
                try
                {   if(RESULT_CODE != "")
                    execute(RESULT_CODE);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                    new Popup(ex.Message,"Error").Show();
                }
            }
            else
            {
                new Popup("Sorry, What ?!", "Error").Show();
                Console.WriteLine("Parse Error");
            }

        }

        private string evaluate(string NT)
        {

            
            foreach(var production in cfg[NT])
            {
                var prod = production.Split(' ');
                if (production.Length == 0)
                {


                    //semantic rules for NT → ϵ
                    var LNT = functions[NT + ":" + production](new DEF());
                    nonterminals.Push(LNT);

                    Console.WriteLine(NT + " -> epsilon ");

                    return "EPSILON";


                }
                else if (isNonTerminal(prod[0]))
                {

                    var result = evaluate(prod[0]);
                    if (result == "INVALID")
                    {
                        goto next;
                    }
                    for (var i = 1; i < prod.Length; i++)
                    {
                        if (isNonTerminal(prod[i]))
                        {
                            evaluate(prod[i]);
                        }
                        else if (prod[i] == l)
                        {
                            terminals.Push(nextlex());
                            l = nexttoken();
                        }
                        else
                        {
                            if (result == "EPSILON")
                            {
                                goto next;
                            }
                            else
                            {
                                error = true;
                                
                                return "INVALID";
                            }

                        }
                    }


                    //semantic rules for NT → A                                       

                    
                    var LNT = functions[NT + ":" + production](new DEF());
                    nonterminals.Push(LNT);

                    Console.WriteLine(NT + " -> " + string.Join(" ", prod));

                    return "NON_TERMINAL";
                }
                else if (prod[0] == l)
                {
                    terminals.Push(nextlex());
                    l = nexttoken();
                    for (var i = 1; i < prod.Length; i++)
                    {
                        if (isNonTerminal(prod[i]))
                            evaluate(prod[i]);
                        else if (prod[i] == l)
                        {
                            //Console.WriteLine("matched", l);
                            terminals.Push(nextlex());
                            l = nexttoken();
                        }
                        else
                            error = true;
                    }

                    //semantic rules for NT → a
                    var LNT = functions[NT + ":" + production](new DEF());
                    nonterminals.Push(LNT);

                    Console.WriteLine(NT + " -> " + string.Join(" ", prod));
                    return "TERMINAL";
                }
                next:;
            }
            return "INVALID";
        }

        private bool isNonTerminal(string str)
        {
            return str != str.ToLower();
        }

        private string nexttoken()
        {
            return tokens[index++];
        }

        private string nextlex()
        {
            return lexemes[index-1];
        }

        private bool isURL(string url) {
            return Regex.IsMatch(url.ToLower(), @"^(http:\/\/www\.|https:\/\/www\.|http:\/\/|https:\/\/)?[a-z0-9]+([\-\.]{1}[a-z0-9]+)*\.[a-z]{2,5}(:[0-9]{1,5})?(\/[^\s]*)?$");
        }

        private bool isPATH(string path)
        {
            return Regex.IsMatch(path.ToLower(), @"^(?:[\w]\:\\)(([a-z_\.\-\s0-9]+[a-z_\-0-9]*\\?)+)?([^\/\\:*?<>|]+)?$") || Regex.IsMatch(path.ToLower(), @"^.*\.(txt|png|jpg|jpeg|gif|doc|docx|pdf)$");
        }

        
        public delegate oup Function<in inp, out oup>(inp arg);

        private void dragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            //e.Handled = true;
            this.DragMove();
            //MessageBox.Show("click");
            Keyboard.ClearFocus();
        }

        private void textBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                enterIsOn = false;
                Console.WriteLine("entered");
                processSentence(textBox.Text);
                //textBox.SelectAll();
            }
        }

        private void processSentence(string str) {
            

            var quotedList = Regex.Matches(str, "\"(.*?)\"");

            for (int i = 0; i < quotedList.Count; i++)
            {
                var unquoted_str = quotedList[i].ToString().Trim('"');
                quoted_data_values.Enqueue(unquoted_str);
                str = str.Replace(quotedList[i].ToString(), "<QDATA>");

            }

            var input = str;
            input = Regex.Replace(input, "folder", "directory", RegexOptions.IgnoreCase);
            Console.WriteLine(input);
            var words = input.Split(' ');

            bool set_appeared = false;

            for (int i = 0; i < words.Length; i++)
            {

                if (words[i] == "set" && !set_appeared)
                {
                    if (i + 1 < words.Length)
                    {
                        set_appeared = true;
                        unquoted_data_values.Enqueue(words[i + 1]);
                        words[i + 1] = "<UQDATA>";
                    }
                }

                if (isURL(words[i]) || isPATH(words[i]))

                {
                    unquoted_data_values.Enqueue(words[i]);
                    words[i] = "<UQDATA>";
                }
                else if (variables.ContainsKey(words[i]))
                {
                    unquoted_data_values.Enqueue(variables[words[i]]);
                    words[i] = "<UQDATA>";
                }
                //Console.WriteLine(words[i]);
            }

            var uriProcessedString = string.Join(" ", words);

            var sentences = MaxentTagger.tokenizeText(new java.io.StringReader(uriProcessedString)).toArray();
            foreach (java.util.ArrayList sentence in sentences)
            {
                //Console.WriteLine(sentence);
                var taggedSentence = tagger.tagSentence(sentence);
                var tags = new string[taggedSentence.size()];
                lexemes = new string[taggedSentence.size()];
                var iterator = taggedSentence.listIterator();
                var i = 0;
                while (iterator.hasNext())
                {
                    var tmp = iterator.next().ToString().Split('/');
                    if (tmp[0] == "<UQDATA>")
                    {
                        try
                        {
                            lexemes[i] = unquoted_data_values.Dequeue();
                        }
                        catch (Exception ex)
                        {

                        }

                    }
                    else if (tmp[0] == "<QDATA>")
                    {
                        try
                        {
                            lexemes[i] = quoted_data_values.Dequeue();
                        }
                        catch (Exception ex)
                        {

                        }

                    }
                    else if (tmp[0] == "=")
                    {
                        tmp[1] = "=";
                        lexemes[i] = tmp[0];
                    }
                    else
                    {
                        lexemes[i] = tmp[0];
                    }
                    tags[i] = tmp[1];
                    i++;
                }
                //Console.WriteLine(SentenceUtils.listToString(taggedSentence, false));
                var inputString = string.Join(" ", tags).ToLower();
                Console.WriteLine(inputString);
                parse(inputString);
                //parse(textBox.Text);
            }

            //parse(input);
        }

        private void Minimize_MouseEnter(object sender, MouseEventArgs e)
        {
            var converter = new System.Windows.Media.BrushConverter();
            var brush = (System.Windows.Media.Brush)converter.ConvertFromString("#FF3F3F41");

            minimize.Background = brush;
            

            //minimize.Background.Opacity = .5;
            //minimize.BorderBrush.Opacity = .5;
        }

        private void Minimize_MouseLeave(object sender, MouseEventArgs e)
        {
            var converter = new System.Windows.Media.BrushConverter();
            var brush = (System.Windows.Media.Brush)converter.ConvertFromString("#FF2D2D30");
            minimize.Background = brush;
            
            //minimize.Background.Opacity = 1;
            //minimize.BorderBrush.Opacity = 1;
        }

       


        private void textBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (textBox.Foreground == System.Windows.Media.Brushes.Gray)
            {
                textBox.Text = "";
                textBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void textBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (textBox.Text == "")
            {
                textBox.Text = "Type here...";
                textBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void minimize_MouseUp(object sender, MouseButtonEventArgs e)
        {
            
            //this.WindowState = WindowState.Minimized;
            animate(this, OpacityProperty, 1, 0, 200,delegate(object obj,EventArgs args) {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            });
        }

        private void minimize_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0;
            animate(this, OpacityProperty, 0, 1, 1000);
        }

        private void textBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                enterIsOn = true;
            }
        }

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            
            //if(!enterIsOn){
            //    Console.WriteLine("damn");
            //    var cmb = (ComboBox)sender;
            //    cmb.IsDropDownOpen = true;
            //    var textbox = cmb.Template.FindName("PART_EditableTextBox", cmb) as TextBox;
            //    cmb.ItemsSource = commands_data.Where(p => string.IsNullOrEmpty(cmb.Text) || p.ToLower().Contains(textbox.Text.ToLower())).ToList();
            //}
            
        }
    }

    public class DEF
    {
        public string value;
        public string code;
        public string type;
        public DEF() {
            value = "";
            code = "";
            type = "";
        }
    }

    

}

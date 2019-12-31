using Newtonsoft.Json;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JamCast.Client
{
    public partial class AuthForm : Form
    {
        public AuthForm()
        {
            InitializeComponent();
        }

        private void _login_Click(object sender, System.EventArgs e)
        {
            _emailAddress.Enabled = false;
            _password.Enabled = false;
            _login.Enabled = false;
            _login.Text = "Logging in...";

            SetBaseAddress();

            var emailAddress = _emailAddress.Text;
            var password = _password.Text;

            Task.Run(async () =>
            {
                try
                {
                    var authenticated = await API.AuthenticateAsync(emailAddress, password);
                    if (authenticated)
                    {
                        AuthResult = API.Credentials;
                        this.Invoke(new Action(() =>
                        {
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }));
                    }
                    else
                    {
                        this.Invoke(new Action(() =>
                        {
                            _emailAddress.Enabled = true;
                            _password.Enabled = true;
                            _login.Enabled = true;
                            _login.Text = "Login";
                            _error.Text = API.Credentials.Error;
                        }));
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        _error.Text = ex.Message;
                    }));
                }
                finally
                {
                    this.Invoke(new Action(() =>
                    {
                        _emailAddress.Enabled = true;
                        _password.Enabled = true;
                        _login.Enabled = true;
                        _login.Text = "Login";
                    }));
                }
            });
        }

        public API.AuthInfo AuthResult { get; private set; }

        private void _emailAddress_KeyUp(object sender, KeyEventArgs e)
        {
            if (_emailAddress.Enabled)
            {
                UpdateLoginButton();
            }
        }

        private void _password_KeyUp(object sender, KeyEventArgs e)
        {
            if (_emailAddress.Enabled)
            {
                UpdateLoginButton();
            }
        }

        private void UpdateLoginButton()
        {
            _login.Enabled = (_emailAddress.Text.Length > 0 &&
                              _password.Text.Length > 0);
        }

        private void _subdomain_Validated(object sender, EventArgs e)
        {
            SetBaseAddress();

            Task.Run(async () =>
            {
                var SiteInfo = await API.GetSiteInfoAsync();
                Invoke(new Action(() =>
                {
                    pictureBox1.LoadAsync(SiteInfo.ImageCover);
                    Text = SiteInfo.SiteName;
                }));

            });
        }

        internal static async Task Authenticate()
        {
            var success = await API.ValidateSession();
            if (!success)
            {
                using (var authForm = new AuthForm())
                {
                    authForm.ShowDialog();
                }
            }

        }

        private void SetBaseAddress()
        {
            var uriBuilder = new UriBuilder(_subdomain.Text + ".jamhost.org")
            {
                Scheme = "https",
                Port = -1,
            };
            API.BaseAddress = uriBuilder.ToString();
        }
    }
}
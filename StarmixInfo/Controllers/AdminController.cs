﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using StarmixInfo.Models;
using StarmixInfo.Models.Data;
using StarmixInfo.Services;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace StarmixInfo.Controllers
{
    [Route("Admin")]
    public class AdminController : Controller
    {
        const string _FileUploadPath = "uploads";

        readonly DataContext _dbContext;
        readonly ILogger<AdminController> _logger;
        readonly IAdminLogon _adminLogon;
        readonly IConfigHelper _configHelper;
        readonly IHostingEnvironment _hostEnvrionment;

        public AdminController(DataContext dbContext, ILogger<AdminController> logger,
                               IAdminLogon adminLogon, IConfigHelper configHelper,
                              IHostingEnvironment hostEnvrironment)
        {
            _dbContext = dbContext;
            _logger = logger;
            _adminLogon = adminLogon;
            _configHelper = configHelper;
            _hostEnvrionment = hostEnvrironment;
        }

        // GET: /<controller>/
        public IActionResult Index(int status = 0)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            // status codes:
            // 1: create succeeded
            // 2: create failed
            // 3: update succeeded
            // 4: update failed
            // 5: delete successed
            // 6: delete failed
            // 7: current project set
            return View((status, (IEnumerable<Project>)_dbContext.Projects.ToList(), _configHelper.CurrentProject));
        }

        #region Login & Password Managment

        // GET: /<controller>/Login
        [HttpGet("Login")]
        public IActionResult Login(bool failed = false)
        {
            if (!_adminLogon.HasSetPassword)
                return RedirectToAction(nameof(SetPassword));
            if (_adminLogon.LoggedIn)
                return RedirectToAction(nameof(Index));
            return View(failed);
        }

        [HttpPost("Login")]
        public IActionResult Login(string password)
        {
            _logger.LogInformation("User attempted to login");
            if (!_adminLogon.HasSetPassword)
            {
                _logger.LogInformation("Admin password not set, redirecting...");
                return RedirectToAction(nameof(SetPassword));
            }
            if (_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User logged in, redirecting...");
                return RedirectToAction(nameof(Index));
            }
            if (_adminLogon.AttemptLogin(password))
            {
                _logger.LogInformation("Login attempt successful");
                return RedirectToAction(nameof(Index));
            }
            _logger.LogInformation("Login attempt failed");
            return RedirectToAction(nameof(Login), new { failed = true });
        }

        [HttpGet("SetPassword")]
        public IActionResult SetPassword()
        {
            if (_adminLogon.HasSetPassword)
            {
                _logger.LogInformation("Password already set, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            return View();
        }

        [HttpPost("SetPassword")]
        public IActionResult SetPassword(string password)
        {
            if (_adminLogon.HasSetPassword)
            {
                _logger.LogInformation("Password already set, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            _adminLogon.SetPassword(password);
            _adminLogon.AttemptLogin(password);
            _logger.LogInformation("User set the admin password");
            return RedirectToAction(nameof(Login));
        }

        #endregion

        #region Project Management

        // GET: /<controller>/SetCurrent/{id}
        [HttpGet("SetCurrent/{id}")]
        public IActionResult SetCurrent(int id)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            _configHelper.CurrentProject = id;
            _logger.LogInformation("Current project set to {0}", id);
            return RedirectToAction(nameof(Index), new { status = 7 });
        }

        // GET: /<controller>/Edit/{id}
        [HttpGet("Edit/{id}")]
        public IActionResult Edit(int id)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            Project proj = _dbContext.Projects.SingleOrDefault(q => q.ProjectID == id);
            if (proj == null)
                return NotFound();
            return View("Project", proj);
        }

        // POST /<controller>/Edit/{id}
        [HttpPost("Edit/{id}")]
        public IActionResult Edit(int id,
                                  [FromForm]string name,
                                  [FromForm]string shortdesc,
                                  [FromForm]string longdesc,
                                  [FromForm]string unityorgid,
                                  [FromForm]string unityprojid,
                                  [FromForm]string gdocfolderid)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            if (SetProjectInfo(id,
                               name,
                               shortdesc,
                               longdesc,
                               unityorgid,
                               unityprojid,
                               gdocfolderid))
            {
                _logger.LogInformation("Project {0} updated", id);
                return RedirectToAction(nameof(Index), new { status = 3 });
            }
            return RedirectToAction(nameof(Index), new { status = 4 });
        }

        // GET: /<controller>/New
        [HttpGet("New")]
        public IActionResult New()
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            return View("Project");
        }

        // POST /<controller>/New
        [HttpPost("New")]
        public IActionResult New([FromForm]string name,
                                 [FromForm]string shortdesc,
                                 [FromForm]string longdesc,
                                 [FromForm]string unityorgid,
                                 [FromForm]string unityprojid,
                                 [FromForm]string gdocfolderid)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            if (SetProjectInfo(null,
                               name,
                               shortdesc,
                               longdesc,
                               unityorgid,
                               unityprojid,
                               gdocfolderid))
            {
                _logger.LogInformation("New project created");
                return RedirectToAction(nameof(Index), new { status = 1 });
            }
            return RedirectToAction(nameof(Index), new { status = 2 });
        }

        // POST /<controller>/Delete/{id}
        [HttpPost("Delete/{id}")]
        public IActionResult Delete(int id)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            try
            {
                Project proj = _dbContext.Projects.SingleOrDefault(q => q.ProjectID == id);
                _dbContext.Projects.Remove(proj);
                _dbContext.SaveChanges();
                return RedirectToAction(nameof(Index), new { status = 5 });
            }
            catch
            {
                return RedirectToAction(nameof(Index), new { status = 6 });
            }
        }

        #region Helpers

        bool SetProjectInfo(int? id,
                            string name,
                            string shortdesc,
                            string longdesc,
                            string unityorgid,
                            string unityprojid,
                            string gdocfolderid)
        {
            try
            {
                Project proj = new Project
                {
                    Name = name,
                    ShortDesc = shortdesc,
                    LongDesc = longdesc,
                    UnityOrgID = unityorgid,
                    UnityProjectID = unityprojid,
                    GDocFolderID = gdocfolderid
                };
                if (id == null)
                {
                    _dbContext.Add(proj);
                }
                else
                {
                    proj.ProjectID = (int)id;
                    _dbContext.Projects.Update(proj);
                }
                _dbContext.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to create/update project (id: {0})", id);
                return false;
            }
        }

        #endregion

        #endregion

        #region Media Management

        [HttpGet("Media")]
        public IActionResult Media(int status = 0)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            List<UploadedFile> files = new List<UploadedFile>();
            foreach (var file in _hostEnvrionment.WebRootFileProvider.GetDirectoryContents(_FileUploadPath))
            {
                UploadedFile ufile = new UploadedFile(file.Name, file.LastModified.DateTime, _FileUploadPath);
                _logger.LogDebug("Found file {0} (uri: {1}, uploaded: {2})", file.PhysicalPath, ufile.UriPath, ufile.UploadDate);
                files.Add(ufile);
            }
            // status codes:
            // 1: file uploaded
            // 3: file deleted
            // 4: unable to delete file
            return View((status, (IEnumerable<UploadedFile>)files));
        }

        [HttpPost("Media/Upload")]
        async public Task<IActionResult> MediaUpload(List<IFormFile> files)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            foreach (var formFile in files)
            {
                if (formFile.Length > 0)
                {
                    string filePath = Path.Combine(_hostEnvrionment.WebRootPath, _FileUploadPath, formFile.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await formFile.CopyToAsync(stream);
                        _logger.LogInformation("Saved file {0} to {1}", formFile.FileName, filePath);
                    }
                }
            }
            return RedirectToAction(nameof(Media), new { status = 1 });
        }

        [HttpPost("Media/Delete")]
        public IActionResult MediaDelete(string filename)
        {
            if (!_adminLogon.LoggedIn)
            {
                _logger.LogInformation("User not logged in, redirecting...");
                return RedirectToAction(nameof(Login));
            }
            IFileInfo file = _hostEnvrionment.WebRootFileProvider.GetFileInfo(Path.Combine(_FileUploadPath, filename));
            if (file.Exists)
            {
                System.IO.File.Delete(file.PhysicalPath);
                _logger.LogInformation("Deleted file {0}", file.PhysicalPath);
                return RedirectToAction(nameof(Media), new { status = 3 });
            }
            return RedirectToAction(nameof(Media), new { status = 4 });
        }

        #endregion
    }
}

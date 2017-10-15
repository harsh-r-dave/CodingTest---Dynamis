using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using WebApplication4.Data;
using WebApplication4.Models;
using WebApplication4.HelperClasses;

namespace WebApplication4.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        // CREATE INSTANCE OF PASSWORD_MANAGER CLASS
        PasswordManager pwdManager = new PasswordManager();

        // VARIABLES TO STORE SESSION VALUES
        const string SessionKeyUserID = "_UserID";
        const string SessionKeyUserEmail = "_UserEmail";

        // CREATE SELECTLIST TO DISPLAY ACCOUNT TYPES IN DROPDOWN LIST
        List<string> AccTypeList = new List<string> { "Business", "Personal" };


        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: User
        public IActionResult Index()
        {
            // STORE USERID IN SESSION TO CONTROL VIEWS
            ViewBag.userID = HttpContext.Session.GetString(SessionKeyUserEmail);
            return View();
        }


        // GET: User/Register
        public IActionResult Register()
        {
            // CREATE SELECTLIST TO DISPLAY ACCOUNT TYPES IN DROPDOWN LIST
            //List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, "Business");

            // CHECK IF USER IS ALREADY LOGGED IN
            if (HttpContext.Session.GetString(SessionKeyUserID) == null)
            {
                return View();
            }
            else
            {
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: User/Register
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register([Bind("acc_type,CompanyName,FirstName,LastName,ProfilePicture,BirthDate,Email,Password")] UserModel userModel, IFormFile file)
        {
            // CREATE SELECTLIST TO DISPLAY ACCOUNT TYPES IN DROPDOWN LIST WHEN THE VIEW IS REDISPLAYED WITH ERROR
            List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, "Business");

            // FETCH THE SELECTED ACCOUNT TYPE FROM DROPDOWN LIST
            string selectedAccType = Request.Form["acc_type"].ToString();
            if (selectedAccType == "Business")
            {
                userModel.FirstName = "";
                userModel.LastName = "";
            }
            else
            {
                userModel.acc_type = "Personal";
                userModel.CompanyName = "";
            }

            // TRY TO REGISTER USER INTO THE DATABASE
            try
            {
                // CHECK EMAIL ADDRESS FOR DUPLICATE RECORD
                var DbEmail = from e in _context.UserModel
                              where e.Email == userModel.Email
                              select e.Email;

                // COMPARE EMAIL ADDRESS IGNORING CASES, RETURN 0 IF BOTH ARE SAME
                if (string.Compare(userModel.Email, DbEmail.FirstOrDefault(), true) == 0)
                {
                    ViewBag.Message = "Email address already exists.";
                    return View();
                }
                else
                {
                    // CHECK FOR USER'S AGE (18 YEARS ~ 6575 DAYS)
                    var age = Utility.AgeSpan(userModel.BirthDate);

                    if (Math.Ceiling(age) <= 6575)
                    {
                        ViewBag.Message = "You should be 18 years or older to register.";
                        return View();
                    }
                    else
                    {
                        // RENAME IMAGE AND STORE IT TO THE SERVER DIRECTORY
                        if (file != null)
                        {
                            //var fileName = Path.GetFileNameWithoutExtension(file.FileName);
                            //var extension = Path.GetExtension(file.FileName);
                            //fileName = fileName + DateTime.Now.ToString("yymmssfff") + extension;

                            // CHECK THE SIZE OF IMAGE 1MB = 1000000 BYTES
                            if (file.Length > 1000000)
                            {
                                ViewBag.Message = "Image size should not exceed 1 MB";
                                return View();
                            }
                            else
                            {
                                var fileName = Utility.GetProfilePictureFileName(file);
                                userModel.ProfilePicture = fileName;
                                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\ProfilePicture", fileName);
                                using (var stream = new FileStream(path, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }
                            }
                        }

                        // SALT AND HASH PASSWORD BEFORE SAVING TO DATABASE
                        String passwordHash = pwdManager.GeneratePasswordHash(userModel.Password, out string salt);
                        userModel.PasswordHash = passwordHash;
                        userModel.PasswordSalt = salt;

                        // SET THE SESSION VALUE AND REGISTER USER
                        HttpContext.Session.SetString(SessionKeyUserEmail, userModel.Email);
                        _context.Add(userModel);
                        await _context.SaveChangesAsync();

                        var DbUserRec = _context.UserModel.Where(u => u.Email == userModel.Email && u.Password == userModel.Password).FirstOrDefault();
                        HttpContext.Session.SetString(SessionKeyUserID, DbUserRec.ID.ToString());
                        return RedirectToAction(nameof(Index));
                    }

                }
            }
            catch (Exception e)
            {
                ViewBag.Message = e.ToString(); // "Something went wrong.";
                return View();

            }
        }


        public IActionResult Login()
        {
            // CHECK IF USER IS ALREADY LOGGED IN
            if (HttpContext.Session.GetString(SessionKeyUserID) != null)
            {
                return RedirectToAction(nameof(Index));
            }
            return View();
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Login(UserModel userModel)
        {
            // 
            // COMPARE EMAIL AND PASSWORD WITH DATABASE VALUES RESPECTING THE CASE
            //var login = _context.UserModel
            //    .Where(u => string.Equals(u.Email, userModel.Email, StringComparison.CurrentCultureIgnoreCase) && string.Equals(u.Password, userModel.Password, StringComparison.CurrentCulture))
            //    .FirstOrDefault();

            var dbUser = _context.UserModel
                .Where(u => string.Equals(u.Email, userModel.Email, StringComparison.CurrentCultureIgnoreCase))
                .FirstOrDefault();

            if (dbUser != null)
            {
                // IF USER EXISTS, CHECK FOR THE PASSWORD
                bool isPasswordCorrect = pwdManager.IsPasswordMatch(userModel.Password, dbUser.PasswordSalt, dbUser.PasswordHash);

                if (isPasswordCorrect)
                {
                    // IF USER IS REGISTERED, SAVE SESSION VALUES AND LOG IN
                    HttpContext.Session.SetString(SessionKeyUserID, dbUser.ID.ToString());
                    HttpContext.Session.SetString(SessionKeyUserEmail, userModel.Email);

                    // REDIRECT TO APPLICATION HOME PAGE
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Username or Password incorrect.");
                }
            }
            else
            {
                ModelState.AddModelError("", "Username or password incorrect.");
            }

            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }


        public IActionResult ManageProfile()
        {
            // FETCH USER ID FROM SESSION
            var id = HttpContext.Session.GetString(SessionKeyUserID);
            var email = HttpContext.Session.GetString(SessionKeyUserEmail);

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            // FETCH USER INFORMATION FROM DATABASE AND SEND TO THE VIEW
            var userModel = _context.UserModel.SingleOrDefault(m => m.ID == Convert.ToInt32(id));
            if (userModel == null)
            {
                return NotFound();
            }

            // FETCH PROFILE PICTURE INFORMATION
            var fileName = userModel.ProfilePicture;
            if (fileName != null)
            {
                ViewBag.ProfileSrc = fileName;
            }

            //List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, userModel.acc_type);

            // SEND SESSION INFO TO CONTROL THE VIEW
            ViewBag.UserID = email;
            return View(userModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageProfile([Bind("ID, acc_type, CompanyName, FirstName, LastName, ProfilePicture, BirthDate, Email")] UserModel userModel, IFormFile file)
        {
            // FETCH USER ID FROM SESSION TO GET THE DATABASE VALUE
            var UserID = HttpContext.Session.GetString(SessionKeyUserID);

            //List<string> AccTypeList = new List<string> { "Business", "Personal" };
            ViewBag.AccType = new SelectList(AccTypeList, userModel.acc_type);

            // USER IS NOT ALLOWED TO UPATE PASSWORD,
            // SO FETCH REGISTERED PASSWORD DETAILS FROM DATABASE,
            // AND STORE IT INTO USERMODEL
            // TO AVOID NULL UPDATION TO PASSWORD FIELD
            var DbPasswordSalt = from e in _context.UserModel
                                 where (e.ID == Convert.ToInt32(UserID))
                                 select e.PasswordSalt;
            var DbPasswordHash = from e in _context.UserModel
                                 where (e.ID == Convert.ToInt32(UserID))
                                 select e.PasswordHash;
            userModel.PasswordHash = DbPasswordHash.FirstOrDefault();
            userModel.PasswordSalt = DbPasswordSalt.FirstOrDefault();


            // IF USER DOESN'T EXIST IN DATABASE
            if (UserID != userModel.ID.ToString())
            {
                return NotFound();
            }

            // TRY UPDATING RECORD
            try
            {
                // GET THE SELECTED ACCOUNT TYPE BY USER
                string SelectedAccType = Request.Form["acc_type"].ToString();
                if (SelectedAccType == "Business")
                {
                    userModel.FirstName = "";
                    userModel.LastName = "";
                }
                else
                {
                    userModel.acc_type = "Personal";
                    userModel.CompanyName = "";
                }

                // FETCH EMAIL OF OTHER USER TO CHECK 
                // IF USER EDITED EMAIL ADDRESS MATCHES 
                // WITH ANY OTHER USER EMAIL ADDRESS
                var DbEmail = from e in _context.UserModel
                              where e.Email == userModel.Email &&
                                            e.ID != Convert.ToInt32(UserID)
                              select e.Email;

                if (userModel.Email == DbEmail.FirstOrDefault())
                {
                    // SEND SESSION INFO TO CONTROL THE VIEW
                    ViewBag.UserID = HttpContext.Session.GetString(SessionKeyUserEmail);
                    ViewBag.Message = "Email address already exists.";
                    ViewBag.MessageCssClass = "alert-info col-md-4";
                    return View();
                }
                else
                {
                    // FETCH EXISTING PROFILE PICTURE NAME
                    var existingProfilePicture = from p in _context.UserModel
                                                 where p.ID == Convert.ToInt32(UserID)
                                                 select p.ProfilePicture;

                    // CHECK FOR THE USER'S AGE (18 YEARS ~ 6575 DAYS)
                    var Age = Utility.AgeSpan(userModel.BirthDate);

                    if (Math.Ceiling(Age) <= 6575)
                    {
                        ViewBag.Message = "You should be 18 years or older to register.";
                        ViewBag.MessageCssClass = "alert-danger col-md-4";
                        ViewBag.ProfileSrc = existingProfilePicture.FirstOrDefault();
                        return View();
                    }
                    else
                    {
                        // RENAME IMAGE AND STORE IT TO THE SERVER DIRECTORY
                        if (file != null)
                        {
                            // DELETE PREVIOUS FILE FROM DIRECTORY
                            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\ProfilePicture");

                            if (existingProfilePicture.FirstOrDefault() != null)
                            {
                                path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\ProfilePicture", existingProfilePicture.FirstOrDefault());
                                if (System.IO.File.Exists(path))
                                {
                                    System.IO.File.Delete(path);
                                }

                            }

                            // UPDATE FILE NAME
                            //var fileName = Path.GetFileNameWithoutExtension(file.FileName);
                            //var extension = Path.GetExtension(file.FileName);
                            //fileName = fileName + DateTime.Now.ToString("yymmssfff") + extension;

                            // CHECK THE SIZE OF IMAGE 1MB = 1000000 BYTES
                            if (file.Length > 1000000)
                            {
                                ViewBag.Message = "Image size should not exceed 1 MB";
                                ViewBag.ProfileSrc = existingProfilePicture.FirstOrDefault();
                                return View(userModel);
                            }
                            var fileName = Utility.GetProfilePictureFileName(file);
                            userModel.ProfilePicture = fileName;
                            path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\ProfilePicture", fileName);
                            using (var stream = new FileStream(path, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            // SEND FILENAME TO <IMG> TAG
                            ViewBag.ProfileSrc = fileName;
                        }
                        else
                        {
                            // IF PROFILE PICTURE NOT UPLOADED, KEEP THE EXISITING FILENAME
                            userModel.ProfilePicture = existingProfilePicture.FirstOrDefault();
                            ViewBag.ProfileSrc = existingProfilePicture.FirstOrDefault();
                        }

                        // UPDATE SESSION INFO FOR ANY CHANGES
                        // AND UPDATE THE DATABASE
                        HttpContext.Session.SetString(SessionKeyUserID, userModel.ID.ToString());
                        HttpContext.Session.SetString(SessionKeyUserEmail, userModel.Email);

                        _context.Update(userModel);
                        await _context.SaveChangesAsync();
                        ViewBag.MessageCssClass = "alert-info col-md-4";
                        ViewBag.Message = "Record updated.";

                        // SEND SESSION INFO TO CONTROL THE VIEW
                        ViewBag.UserID = HttpContext.Session.GetString(SessionKeyUserEmail);
                        ViewBag.MessageCssClass = "";
                        return View(userModel);
                    }
                }
            }
            catch (Exception e)
            {
                //ViewBag.Message = e.ToString();
                ViewBag.Message = "Something went wrong.";
                ViewBag.MessageCssClass = "alert-danger col-md-4";
            }
            //return RedirectToAction(nameof(Index));
            return View(userModel);
        }
    }

    // PASSWORD ENCRYPTION CLASSES
    // REFERENCE: WWW.CODEPROJECT.COM
    public static class SaltGenerator
    {
        private static RNGCryptoServiceProvider _cryptoServiceProvider = null;
        private const int SALT_SIZE = 24;
        static SaltGenerator()
        {
            _cryptoServiceProvider = new RNGCryptoServiceProvider();
        }

        public static string GetSaltString()
        {
            // TO STORE SALT BYTES
            byte[] saltBytes = new byte[SALT_SIZE];

            // GENERATE THE SALT
            _cryptoServiceProvider.GetNonZeroBytes(saltBytes);

            // CONVERT SALT TO STRING
            string saltString = Encoding.ASCII.GetString(saltBytes);
            return saltString;
        }
    }

    public class HashGenerator
    {
        public string GetPasswordHashAndSalt(string password)
        {
            // SHA256 algorithm to 
            // generate the hash from this salted password
            SHA256 sha = new SHA256CryptoServiceProvider();
            byte[] dataBytes = Encoding.ASCII.GetBytes(password);
            byte[] resultBytes = sha.ComputeHash(dataBytes);

            return Encoding.ASCII.GetString(resultBytes);
        }
    }

    public class PasswordManager
    {
        HashGenerator _hashComputer = new HashGenerator();

        public string GeneratePasswordHash(string plainTextPassword, out string salt)
        {
            salt = SaltGenerator.GetSaltString();

            string finalString = plainTextPassword + salt;

            return _hashComputer.GetPasswordHashAndSalt(finalString);
        }

        public bool IsPasswordMatch(string password, string salt, string hash)
        {
            string finalString = password + salt;
            return hash == _hashComputer.GetPasswordHashAndSalt(finalString);
        }
    }
}
<?php
    $db_host		= 'localhost';
	$db_user		= 'atmstudies';
	$db_pass		= 'zbz82FySFNWvv75y';
	$db_database	= 'atmstudies'; 

	$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');
	mysql_select_db($db_database);

	session_start();

	$token = $_GET['token'];

	if(!isset($_POST['x']))
	{
		$sql = "SELECT `User` FROM `cmrapplogin` WHERE `Token` = \"$token\"";
		$result = mysql_query($sql, $link);

		if (!$result) echo mysql_error();
		$row = mysql_fetch_assoc($result);

		$email = $row["User"];

		if ($email != '')
		{
			$_SESSION['email'] = $email;
			echo '<form method="post">
			Enter your new password:  <input type="password" name="x" />
			<input type="submit" value="Change Password">
			</form>
			';
		}
		else 
		{
			die("Invalid link or Password already changed!");
		}
	}

	if (isset($_POST['x']) && isset($_SESSION['email']))
	{
		$pass = $_POST['x'];
		$email = $_SESSION['email'];

		$ky = base64_decode("hujsha8yushhjhey7t35549jnnbhjQQQkmikM1xMxMN=");
		$iv = base64_decode("bhhuhu72sttDQertT5UxjihubugDFRdAxpllxxdfxgh=");

		$o1 = $pass; //base64_encode(mcrypt_encrypt(MCRYPT_RIJNDAEL_256, $ky, $pass, MCRYPT_MODE_CBC, $iv));
	
		$sql = "UPDATE `cmrapplogin` SET `Password` = \"$o1\" WHERE `User` = \"$email\"";
		$result = mysql_query($sql, $link);
		if ($result)
		{
		    $sql = "UPDATE `cmpapplogin` Set `Token` = \"Used\" WHERE `User` = \"$email\"";
			mysql_query($sql, $link);
			echo "Your password has been changed successfully.";
		}
		else
		{
			echo "An error occurred!";
		}
	}
?>

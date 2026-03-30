<?php
    $db_host		= 'localhost';
	$db_user		= 'atmstudies';
	$db_pass		= 'zbz82FySFNWvv75y';
	$db_database	= 'atmstudies'; 

	$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');
	mysql_select_db($db_database);

	$ky = base64_decode("hujsha8yushhjhey7t35549jnnbhjQQQkmikM1xMxMN=");
	$iv = base64_decode("bhhuhu72sttDQertT5UxjihubugDFRdAxpllxxdfxgh=");

	$x1 = str_replace(" ", "+", $_POST["x1"]);
	$i1 = $x1; //strstr(mcrypt_decrypt(MCRYPT_RIJNDAEL_256, $ky, base64_decode($x1), MCRYPT_MODE_CBC, $iv), "\0", TRUE);

	$sql = "SELECT * FROM `cmrapplogin` WHERE `User` = \"$i1\"";

	$result = mysql_query($sql, $link);
	    
    $msg = (!$result ? "Error: Database query failed!" : ((mysql_num_rows($result) == 1) ? "Ok" : "Error: Account does not exist!"));

	function getRandomString($length) 
	{
		$validCharacters = "ABCDEFGHIJKLMNPQRSTUXYVWZ123456789";
		$validCharNumber = strlen($validCharacters);
		$result = "";
		for ($i = 0; $i < $length; $i++) {
			$index = mt_rand(0, $validCharNumber - 1);
			$result .= $validCharacters[$index];
		}
		return $result;
	}

	if ($msg == "Ok")
	{
		$token = getRandomString(10);
		$sql = "UPDATE `cmrapplogin` SET `Token` = \"$token\" WHERE `User` = \"$i1\"";
		$result = mysql_query($sql, $link);
		$msg = '
			<html>
			<head>
			<title>ATMML Password Reset</title>
			</head>
			<body>
			<p>Click on the following link to reset your password:   <a href="http://www.capitalmarketsresearch.com/reset-password.php?token='.$token.'">Reset Password</a></p>
			</body>
			</html>
		';
	}
	else
	{
		$msg = "Error: Password reset failed!";
	}
	    
	$output = $msg; //base64_encode(mcrypt_encrypt(MCRYPT_RIJNDAEL_256, $ky, $msg, MCRYPT_MODE_CBC, $iv));
	echo $output; 
?>

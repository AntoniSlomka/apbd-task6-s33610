namespace APBD_TASK6.DTOs
{
    public class AppointmentDetailsDto
    {
        //patient email, phone number, doctor license number,
        //internal notes, and record creation date.
        public int IdAppointment { get; set; }

        public DateTime AppointmentDate { get; set; }

        public int IdPatient { get; set; }

        public string PatientFirstName { get; set; } = string.Empty;

        public string PatientLastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public int IdDoctor { get; set; }

        public string DoctorFirstName { get; set; } = string.Empty;
        
        public string DoctorLastName { get; set; } = string.Empty;
        
        public string LicenseNUmber { get; set; } = string.Empty;    

        public DateTime CreatedAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
        
        public string InternalNotes { get; set; } = string.Empty;
        
    }
}
